using JMS;
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
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
                }
            }
        }
        bool _finished = false;
        public string GatewayAddress { get; }
        public int GatewayPort { get; }
        Dictionary<string, string> _Header = new Dictionary<string, string>();
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
                    Header = this.GetCommandHeader(),
                });
                var serviceLocations = netclient.ReadServiceObject<RegisterServiceRunningInfo[]>();
               
                return serviceLocations;
            }
        }

        public void SetHeader(string key,string value)
        {
            if (key == "TranId")
                throw new Exception("key='TranId' is not allow");
            _Header[key] = value;
        }

        public Dictionary<string,string> GetCommandHeader()
        {
            var header = new Dictionary<string, string>();
            header["TranId"] = this.TransactionId;
            foreach (var pair in _Header)
            {
                header[pair.Key] = pair.Value;
            }
            return header;
        }

        public T GetMicroService<T>() where T : IImplInvoker
        {
            var classType = typeof(T);
            
            var invoker = new Invoker(this, classType.GetCustomAttribute<InvokerInfoAttribute>().ServiceName);
            if (invoker.Init())
                return (T)Activator.CreateInstance(classType, new object[] { invoker });
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
                var connect = _Connects[i];
                try
                {
                    connect.NetClient.WriteServiceData(new InvokeCommand()
                    {
                        Type = InvokeType.HealthyCheck,
                    });
                    var ret = connect.NetClient.ReadServiceObject<InvokeResult>();
                    if(ret.Success == false)
                    {
                        //有人不同意提交事务
                        //把提交更改为回滚
                        invokeType = InvokeType.RollbackTranaction;
                    }
                }
                catch (Exception ex)
                {
                    connect.NetClient.Dispose();
                    errors.Add(new TransactionException(connect.ServiceLocation, ex.Message));
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
                    var connect = _Connects[i];
                    bool reconnect = false;
                    while (true)
                    {
                        try
                        {
                            if (reconnect)
                            {
                                Thread.Sleep(1000);
                                connect.ReConnect();
                            }

                            if (errors.Count == 0)
                            {
                                connect.NetClient.WriteServiceData(new InvokeCommand()
                                {
                                    Type = invokeType,
                                    Header = this.GetCommandHeader()
                                });
                                connect.NetClient.ReadServiceObject<InvokeResult>();
                            }
                            else
                            {
                                errors.Add(new TransactionException(connect.ServiceLocation, "cancel"));
                            }
                            break;
                        }
                        catch (SocketException ex)
                        {
                            if (connect.ReConnectCount < 10)
                            {
                                connect.NetClient.Dispose();
                                reconnect = true;
                            }
                            else
                            {
                                errors.Add(new TransactionException(connect.ServiceLocation, ex.Message));
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new TransactionException(connect.ServiceLocation, ex.Message));
                            break;
                        }
                    }

                    if (connect.NetClient.HasSocketException == false)
                    {
                        NetClientPool.AddClientToPool(connect.NetClient);
                    }
                    else
                    {
                        connect.NetClient.Dispose();
                    }
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
