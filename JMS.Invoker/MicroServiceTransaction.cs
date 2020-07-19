using JMS;
using JMS.Common.Dtos;
using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
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
        public NetAddress GatewayAddress { get; }
        public NetAddress ProxyAddress { get; }

        Dictionary<string, string> _Header = new Dictionary<string, string>();
        public X509Certificate2 GatewayClientCertificate { get;private set; }
        public X509Certificate2 ServiceClientCertificate { get; private set; }

        ILogger<MicroServiceTransaction> _logger;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gatewayAddress">网关ip</param>
        /// <param name="port">网关端口</param>
        /// <param name="proxyAddress"></param>
        /// <param name="logger">日志对象，用于在事务发生意外时，记录详细信息</param>
        /// <param name="gatewayClientCert">与网关互通的证书</param>
        /// <param name="serviceClientCert">与微服务互通的证书</param>
        public MicroServiceTransaction(string gatewayAddress, int port, NetAddress proxyAddress = null, ILogger<MicroServiceTransaction> logger = null, X509Certificate2 gatewayClientCert = null, X509Certificate2 serviceClientCert = null)
        {
            GatewayAddress = new NetAddress(gatewayAddress, port);
            GatewayClientCertificate = gatewayClientCert;
            ServiceClientCertificate = serviceClientCert;
            this.ProxyAddress = proxyAddress;
            _logger = logger;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gatewayAddresses">多个集群网关地址</param>
        /// <param name="proxyAddress"></param>
        /// <param name="logger">日志对象，用于在事务发生意外时，记录详细信息</param>
        /// <param name="gatewayClientCert">与网关互通的证书</param>
        /// <param name="serviceClientCert">与微服务互通的证书</param>
        public MicroServiceTransaction(NetAddress[] gatewayAddresses,NetAddress proxyAddress = null, ILogger<MicroServiceTransaction> logger = null,  X509Certificate2 gatewayClientCert = null, X509Certificate2 serviceClientCert = null)
        {
            _logger = logger;
            this.ProxyAddress = proxyAddress;
            //先找到master网关
            NetAddress masterAddress = null;
            if (gatewayAddresses.Length == 1)
            {
                masterAddress = gatewayAddresses[0];
            }
            else
            {
                ManualResetEvent waitobj = new ManualResetEvent(false);
                int errCount = 0;
                for (int i = 0; i < gatewayAddresses.Length; i++)
                {
                    var addr = gatewayAddresses[i];
                    Task.Run(() =>
                    {
                        try
                        {
                            using (var client = new ProxyClient( this.ProxyAddress , addr, gatewayClientCert))
                            {
                                client.WriteServiceData(new GatewayCommand
                                {
                                    Type = CommandType.FindMaster
                                });
                                var ret = client.ReadServiceObject<InvokeResult>();
                                if (ret.Success == true && masterAddress == null)
                                {
                                    masterAddress = addr;
                                    waitobj.Set();
                                }
                                else
                                {
                                    throw new Exception();
                                }
                            }
                        }
                        catch
                        {
                            Interlocked.Increment(ref errCount);
                            if (errCount == gatewayAddresses.Length)
                                waitobj.Set();
                        }
                    });
                }
                waitobj.WaitOne();
                waitobj.Dispose();
            }

            if (masterAddress == null)
                throw new MissMasterGatewayException("无法找到主网关");
            GatewayAddress = masterAddress;
            GatewayClientCertificate = gatewayClientCert;
            ServiceClientCertificate = serviceClientCert;
        }
        public RegisterServiceRunningInfo[] ListMicroService(string serviceName)
        {
            using (var netclient = new ProxyClient( this.ProxyAddress, GatewayAddress, GatewayClientCertificate))
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
                throw new TransactionArrayException(errors, $"有{errors.Count}个服务提交事务失败");
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
                    errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
                }
                
            });

            if (errors.Count > 0)
            {
                foreach( var client in _Connects )
                {
                    try
                    {
                        client.NetClient.WriteServiceData(new InvokeCommand()
                        {
                            Type = InvokeType.RollbackTranaction,
                            Header = this.GetCommandHeader()
                        });
                    }
                    catch
                    {
                    }
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
                                connect.ReConnect(this);
                            }

                            if (errors.Count == 0)
                            {
                                connect.NetClient.WriteServiceData(new InvokeCommand()
                                {
                                    Type = invokeType,
                                    Header = this.GetCommandHeader()
                                });
                                var ret = connect.NetClient.ReadServiceObject<InvokeResult>();
                                if( ret.Success == false && invokeType == InvokeType.CommitTranaction)
                                {
                                    errors.Add(new TransactionException(connect.InvokingInfo, "事务已被回滚"));
                                }
                            }
                            else
                            {
                                errors.Add(new TransactionException(connect.InvokingInfo, "cancel"));
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
                                errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
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

                if(errors.Count > 0)
                {
                    foreach (var err in errors)
                        _logger?.LogError(err, $"事务:{TransactionId}发生错误");
                }
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
