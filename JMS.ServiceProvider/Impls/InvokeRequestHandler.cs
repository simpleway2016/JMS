using JMS.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using System.Linq;

namespace JMS.Impls
{
    class InvokeRequestHandler : IRequestHandler
    {
        MicroServiceHost _MicroServiceProvider;
        TransactionDelegateCenter _transactionDelegateCenter;
        ILogger<InvokeRequestHandler> _logger;
        public InvokeRequestHandler(TransactionDelegateCenter transactionDelegateCenter,
            ILogger<InvokeRequestHandler> logger,
            MicroServiceHost microServiceProvider)
        {
            _transactionDelegateCenter = transactionDelegateCenter;
            _MicroServiceProvider = microServiceProvider;
            _logger = logger;
        }

        public InvokeType MatchType => InvokeType.Invoke;

        public void Handle(NetClient netclient, InvokeCommand cmd)
        {
            TransactionDelegate transactionDelegate = null;
            MicroServiceControllerBase controller = null;
            object[] parameters = null;

            try
            {
                MicroServiceControllerBase.RequestingCommand.Value = cmd;
                var controllerTypeInfo = _MicroServiceProvider.ServiceNames[cmd.Service];
                controller = (MicroServiceControllerBase)_MicroServiceProvider.ServiceProvider.GetService(controllerTypeInfo.Type);
                controller.NetClient = netclient;
                controller._keyLocker = _MicroServiceProvider.ServiceProvider.GetService<IKeyLocker>();
                _logger?.LogDebug("invoke service:{0} method:{1} parameters:{2}", cmd.Service, cmd.Method, cmd.Parameters);
                var method = controllerTypeInfo.Methods.FirstOrDefault(m=>m.Name == cmd.Method);
                if (method == null)
                    throw new Exception($"{cmd.Service}没有提供{cmd.Method}方法");
                var parameterInfos = method.GetParameters();
                object result = null;

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
                    result = method.Invoke(controller, parameters);
                }
                else
                {
                    for (int i = startPIndex, index = 0; i < parameters.Length && index < cmd.Parameters.Length; i++, index++)
                    {
                        string pvalue = cmd.Parameters[index];
                        try
                        {
                            parameters[i] = Newtonsoft.Json.JsonConvert.DeserializeObject(pvalue, parameterInfos[i].ParameterType);
                        }
                        catch (Exception)
                        {
                            _logger?.LogError("转换参数出错，name:{0} cmd:{1}", parameterInfos[i].Name, cmd.ToJsonString());
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
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = true,
                    SupportTransaction = supportTran,
                    Data = result

                });

                if (!supportTran)
                {
                    return;
                }

                netclient.ReadTimeout = 0;
                while (true)
                {
                    cmd = netclient.ReadServiceObject<InvokeCommand>();
                    if (cmd.Type == InvokeType.CommitTranaction)
                    {
                        if (transactionDelegate != null && transactionDelegate.CommitAction != null)
                            transactionDelegate.CommitAction();
                        netclient.WriteServiceData(new InvokeResult() { Success = true });
                        return;
                    }
                    else if (cmd.Type == InvokeType.RollbackTranaction)
                    {
                        if (transactionDelegate != null && transactionDelegate.RollbackAction != null)
                            transactionDelegate.RollbackAction();
                        netclient.WriteServiceData(new InvokeResult() { Success = true });
                        return;
                    }
                    else if (cmd.Type == InvokeType.HealthyCheck)
                    {
                        netclient.WriteServiceData("ok");
                    }
                }
            }
            catch (SocketException)
            {
                if (transactionDelegate != null)
                {
                    //连接意外中断，交给TransactionDelegateCenter事后处理
                    _logger?.LogInformation("连接意外中断，交给TransactionDelegateCenter事后处理,事务id:{0}", transactionDelegate.TransactionId);
                    _transactionDelegateCenter.AddTransactionDelegate(transactionDelegate);
                    transactionDelegate = null;
                }
                return;
            }
            catch (ResponseEndException)
            {
                return;
            }
            catch (Exception ex)
            {
                try
                {
                    controller?.InvokeError(cmd.Method, parameters, ex);
                }
                catch (ResponseEndException)
                {
                    return;
                }


                _logger?.LogError(ex, ex.Message);

                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false,
                    Error = ex.Message
                });
                return;
            }
        }
    }
}
