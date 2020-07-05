using JMS;
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc
{
    /// <summary>
    /// 微服务事务管理
    /// </summary>
    public class MicroServiceTransaction : IDisposable
    {
        List<InvokeConnect> _Connects = new List<InvokeConnect>();
        List<Task> _Tasks = new List<Task>();

        private string _TransactionId;
        public string TransactionId
        {
            get => _TransactionId;
            set
            {
                if (_TransactionId != value)
                {
                    _TransactionId = value;
                    this.Header["TranId"] = value;
                }
            }
        }
        bool _finished = false;
        public string GatewayAddress { get; }
        public int GatewayPort { get; }
        public Dictionary<string, string> Header = new Dictionary<string, string>();
        public MicroServiceTransaction(string gatewayAddress, int port)
        {
            GatewayAddress = gatewayAddress;
            GatewayPort = port;
        }
        public RegisterServiceRunningInfo[] ListMicroService(string serviceName)
        {
            using (var netclient = new NetClient(GatewayAddress, GatewayPort))
            {
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetAllServiceProviders,
                    Content = serviceName,
                    Header = this.Header
                });
                var serviceLocations = netclient.ReadServiceObject<RegisterServiceRunningInfo[]>();
               
                return serviceLocations;
            }
        }

        public T GetMicroService<T>(string serviceName) where T : IImplInvoker
        {
            var invoker = new Invoker(this, serviceName);
            if (invoker.Init())
                return (T)Activator.CreateInstance(typeof(T), new object[] { invoker });
            return default(T);
        }


        public IMicroService GetMicroService( string serviceName)
        {
            var invoker = new Invoker(this, serviceName);
            if (invoker.Init())
                return invoker;
            return null;
        }

      

        internal void AddConnect(InvokeConnect   connect)
        {
            lock(_Connects)
            {
                _Connects.Add(connect);
            }           
        }
        internal void AddTask(Task task)
        {
            lock (_Tasks)
            {
                _Tasks.Add(task);
            }
        }

        void waitTasks()
        {
            Task.WaitAll(_Tasks.ToArray());
        }

        public void Commit()
        {
            if (_finished)
                return;
            _finished = true;
            var errors = endResponse(InvokeType.CommitTranaction);
            if (errors.Count > 0)
                throw new TransactionArrayException(errors, "commit transaction error");
        }

        List<TransactionException> endResponse(InvokeType invokeType)
        {
            waitTasks();

            List<TransactionException> errors = new List<TransactionException>(_Connects.Count);
            //健康检查
            Parallel.For(0, _Connects.Count, (i) => {
                var client = _Connects[i];
                try
                {
                    client.NetClient.WriteServiceData(new InvokeCommand()
                    {
                        Type = InvokeType.HealthyCheck,
                    });
                    var ret = client.NetClient.ReadServiceObject<InvokeResult>();
                    if(ret.Success == false)
                    {
                        //有人不同意提交事务
                        //把提交更改为回滚
                        invokeType = InvokeType.RollbackTranaction;
                    }
                }
                catch (Exception ex)
                {
                    client.NetClient.Dispose();
                    errors.Add(new TransactionException(client.ServiceLocation, ex.Message));
                }
                
            });

            if (errors.Count > 0)
            {
                foreach( var client in _Connects )
                {
                    client.NetClient.Dispose();
                }
                if(invokeType == InvokeType.CommitTranaction)
                    throw new TransactionException(null, "提交事务时，有连接中断，所有事务将回滚");
                else
                    throw new TransactionException(null, "回滚事务时，有连接中断，所有事务将稍后回滚");
            }

            if (errors.Count == 0)
            {
                Parallel.For(0, _Connects.Count, (i) => {
                    var client = _Connects[i];
                    bool reconnect = false;
                    while (true)
                    {
                        try
                        {
                            if (reconnect)
                            {
                                Thread.Sleep(1000);
                                client.ReConnect();
                            }

                            if (errors.Count == 0)
                            {
                                client.NetClient.WriteServiceData(new InvokeCommand()
                                {
                                    Type = invokeType,
                                    Header = this.Header
                                });
                                client.NetClient.ReadServiceObject<InvokeResult>();
                            }
                            else
                            {
                                errors.Add(new TransactionException(client.ServiceLocation, "cancel"));
                            }
                            break;
                        }
                        catch (SocketException ex)
                        {
                            if (client.ReConnectCount < 10)
                            {
                                client.NetClient.Dispose();
                                reconnect = true;
                            }
                            else
                            {
                                errors.Add(new TransactionException(client.ServiceLocation, ex.Message));
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new TransactionException(client.ServiceLocation, ex.Message));
                            break;
                        }
                    }
                    client.NetClient.Dispose();
                });
            }
           

            _Connects.Clear();
            _Tasks.Clear();

            return errors;
           
        }

        public void Rollback()
        {
            if (_finished)
                return;
            _finished = true;

           var errors = endResponse(InvokeType.RollbackTranaction);
            if (errors.Count > 0)
                throw new TransactionArrayException(errors, "rollback transaction error");
        }

        public void Dispose()
        {
            Rollback();
        }
    }
}
