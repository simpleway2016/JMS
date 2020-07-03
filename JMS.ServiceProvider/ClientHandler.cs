using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Way.Lib;

namespace JMS
{
    class ClientHandler
    {
        internal Way.Lib.NetStream Client;
        MicroServiceProvider _ServiceProvider;
        ILogger<ClientHandler> _logger;
        public ClientHandler(MicroServiceProvider serviceProvider)
        {
            _ServiceProvider = serviceProvider;
            _logger = _ServiceProvider.ServiceProvider.GetService<ILogger<ClientHandler>>();
        }

     
        public void Handle(Socket socket)
        {
            Client = new Way.Lib.NetStream(socket);
            Client.ReadTimeout = 0;
            TransactionDelegate transactionDelegate = null;
            MicroServiceController controller = null;
            while (true)
            {
                try
                {
                    var cmd = Client.ReadServiceObject<InvokeCommand>();
                    if (cmd.Type == InvokeType.CommitTranaction)
                    {
                        if(transactionDelegate == null)
                        {
                            _ServiceProvider.ServiceProvider.GetService<TransactionDelegateCenter>().Commit(cmd.Header["TranId"]);
                            Client.WriteServiceData(new InvokeResult() { Success = true });
                            Client.Dispose();
                            return;
                        }

                        if (transactionDelegate != null && transactionDelegate.CommitAction != null)
                            transactionDelegate.CommitAction();
                        Client.WriteServiceData(new InvokeResult() { Success = true});
                        Client.Dispose();
                        return;
                    }
                    else if (cmd.Type == InvokeType.RollbackTranaction)
                    {
                        if (transactionDelegate == null)
                        {
                            _ServiceProvider.ServiceProvider.GetService<TransactionDelegateCenter>().Rollback(cmd.Header["TranId"]);
                            Client.WriteServiceData(new InvokeResult() { Success = true });
                            Client.Dispose();
                            return;
                        }

                        if (transactionDelegate != null && transactionDelegate.RollbackAction != null)
                            transactionDelegate.RollbackAction();
                        Client.WriteServiceData(new InvokeResult() { Success = true });
                        Client.Dispose();
                        return;
                    }

                    MicroServiceController.RequestingCommand.Value = cmd;
                    var controllerType = _ServiceProvider.ServiceNames[cmd.Service];
                    controller = (MicroServiceController)_ServiceProvider.ServiceProvider.GetService(controllerType);
                    controller.NetClient = Client;
                    _logger?.LogDebug("invoke service:{0} method:{1} parameters:{2}", cmd.Service, cmd.Method, cmd.Parameters);
                    var method = controllerType.GetMethod(cmd.Method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var parameterInfos = method.GetParameters();
                    object result = null;
                    object[] parameters = null;
                    int startPIndex = 0;
                    if (parameterInfos.Length > 0)
                    {
                        parameters = new object[parameterInfos.Length];
                        if (parameterInfos[0].ParameterType == typeof(TransactionDelegate))
                        {
                            startPIndex = 1;
                            parameters[0] = transactionDelegate = new TransactionDelegate(cmd.Header["TranId"]);
                            transactionDelegate.RequestCommand = cmd;
                        }
                    }

                    controller.BeforeAction(cmd.Method, parameters);
                    if (parameterInfos.Length == 0)
                    {
                        result = method.Invoke(controller,parameters );
                    }
                    else
                    {
                        for(int i = startPIndex, index = 0; i < parameters.Length && index < cmd.Parameters.Length; i ++,index++)
                        {
                            string pvalue = cmd.Parameters[index];
                            try
                            {
                                parameters[i] = Newtonsoft.Json.JsonConvert.DeserializeObject(pvalue, parameterInfos[i].ParameterType);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("转换参数出错，name:{0} cmd:{1}" , parameterInfos[i].Name , cmd.ToJsonString());
                            }
                           
                        }
                        result = method.Invoke(controller, parameters);
                    }
                    controller.AfterAction(cmd.Method, parameters);

                    var supportTran = false;
                    if (transactionDelegate != null && (transactionDelegate.CommitAction != null || transactionDelegate.RollbackAction != null))
                    {
                        supportTran = true;
                    }
                    else if (controller.TransactionControl != null && (controller.TransactionControl.CommitAction != null || controller.TransactionControl.RollbackAction != null))
                    {
                        transactionDelegate = controller.TransactionControl;
                        transactionDelegate.RequestCommand = cmd;
                        supportTran = true;
                    }
                    Client.WriteServiceData(new InvokeResult
                    {
                        Success = true,
                        SupportTransaction = supportTran,
                        Data = result

                    });

                    if(!supportTran)
                    {
                        Client.Dispose();
                        return;
                    }
                }
                catch(SocketException ex)
                {
                    if (transactionDelegate != null )
                    {
                        //连接意外中断，交给TransactionDelegateCenter事后处理
                        _logger?.LogInformation("连接意外中断，交给TransactionDelegateCenter事后处理,事务id:{0}", transactionDelegate.TransactionId);
                        _ServiceProvider.ServiceProvider.GetService<TransactionDelegateCenter>().AddTransactionDelegate(transactionDelegate);
                        transactionDelegate = null;
                    }
                    Client.Dispose();
                    return;
                }
                catch (ResponseEndException)
                {
                    Client.Dispose();
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);

                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }
                    Client.WriteServiceData(new InvokeResult
                    {
                        Success = false,
                        Error = ex.Message
                    });

                    Client.Dispose();
                    return;
                }               
            }
        }
    }
}
