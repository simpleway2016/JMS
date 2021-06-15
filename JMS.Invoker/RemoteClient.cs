using JMS;

using JMS.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    /// <summary>
    /// 微服务客户端
    /// </summary>
    public class RemoteClient : IDisposable, IRemoteClient
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
        /// <summary>
        /// 是否支持事务，如果为false，这之后调用的微服务端会直接提交事务。默认为true
        /// </summary>
        private bool _SupportTransaction = false;


        private int _Timeout = 30000;
        /// <summary>
        /// 超时时间，默认30000ms
        /// </summary>
        public int Timeout
        {
            get => _Timeout;
            set
            {
                if (_Timeout != value)
                {
                    _Timeout = value;
                }
            }
        }

        static Way.Lib.Collections.ConcurrentList<NetAddress> HistoryMasterAddressList = new Way.Lib.Collections.ConcurrentList<NetAddress>();

        public NetAddress GatewayAddress { get; private set; }
        public NetAddress ProxyAddress { get; }
        NetAddress[] _allGateways;

        Dictionary<string, string> _Header = new Dictionary<string, string>();
        public X509Certificate2 GatewayClientCertificate { get;  set; }
        public X509Certificate2 ServiceClientCertificate { get;  set; }

        ILogger<RemoteClient> _logger;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gatewayAddress">网关ip</param>
        /// <param name="port">网关端口</param>
        /// <param name="proxyAddress"></param>
        /// <param name="logger">日志对象，用于在事务发生意外时，记录详细信息</param>
        /// <param name="gatewayClientCert">与网关互通的证书</param>
        /// <param name="serviceClientCert">与微服务互通的证书</param>
        public RemoteClient(string gatewayAddress, int port, NetAddress proxyAddress = null, ILogger<RemoteClient> logger = null, X509Certificate2 gatewayClientCert = null, X509Certificate2 serviceClientCert = null)
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
        public RemoteClient(NetAddress[] gatewayAddresses,NetAddress proxyAddress = null, ILogger<RemoteClient> logger = null,  X509Certificate2 gatewayClientCert = null, X509Certificate2 serviceClientCert = null)
        {
            _logger = logger;
            this.ProxyAddress = proxyAddress;
            GatewayClientCertificate = gatewayClientCert;
            ServiceClientCertificate = serviceClientCert;

            //先找到master网关
            _allGateways = gatewayAddresses;
            findMasterGateway();
        }

        /// <summary>
        /// 获取当前微服务列表
        /// </summary>
        /// <param name="serviceName">服务名称，空表示所有服务</param>
        /// <returns></returns>
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

        /// <summary>
        /// 获取指定某个微服务的地址列表
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns></returns>
        public RegisterServiceLocation[] ListMicroServiceLocations(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new Exception("serviceName is empty");

            var infos = ListMicroService(serviceName);
            var ret = new RegisterServiceLocation[infos.Length];
            for(int i = 0; i < infos.Length; i ++)
            {
                var info = infos[i];
                ret[i] = new RegisterServiceLocation { 
                    Host = info.Host,
                    Port = info.Port,
                    ServiceAddress = info.ServiceAddress,
                    Description = info.Description
                };
            }
            return ret;
        }
        /// <summary>
        /// 强行要求微服务释放锁定的key（慎用）
        /// </summary>
        /// <param name="serviceAddress"></param>
        /// <param name="key"></param>
        public void UnLockKeyAnyway(NetAddress serviceAddress,string key)
        {
            using (var netclient = new ProxyClient(this.ProxyAddress, serviceAddress, ServiceClientCertificate))
            {
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = InvokeType.UnlockKeyAnyway,
                    Method = key
                });
                netclient.ReadServiceObject<InvokeResult>();
            }
        }

        /// <summary>
        /// 获取指定微服务器当前锁定的key
        /// </summary>
        /// <param name="serviceAddress"></param>
        /// <returns></returns>
        public string[] GetLockedKeys(NetAddress serviceAddress)
        {
            using (var netclient = new ProxyClient(this.ProxyAddress, serviceAddress, ServiceClientCertificate))
            {
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = InvokeType.GetAllLockedKeys,
                });
                var ret = netclient.ReadServiceObject<InvokeResult<string[]>>();
                return ret.Data;
            }
        }

        public void SetHeader(string key,string value)
        {
            if (key == "TranId")
                throw new Exception("key='TranId' is not allow");
            else if (key == "Tran")
                throw new Exception("key='Tran' is not allow");
            _Header[key] = value;
        }

        public Dictionary<string,string> GetCommandHeader()
        {
            var header = new Dictionary<string, string>();
            header["TranId"] = this.TransactionId;

            if (_SupportTransaction == false)
                header["Tran"] = "0";

            foreach (var pair in _Header)
            {
                header[pair.Key] = pair.Value;
            }
            return header;
        }

        void findMasterGateway()
        {
            if (_allGateways == null || _allGateways.Length == 1)
            {
                if(_allGateways != null)
                    GatewayAddress = _allGateways[0];
                return;
            }

            //先从历史主网关选出一个
            var historyMaster = HistoryMasterAddressList.FirstOrDefault(m => _allGateways.Any(g => g==m));
            if(historyMaster != null)
            {
                GatewayAddress = historyMaster;
                return;
            }

            NetAddress masterAddress = null;
            ManualResetEvent waitobj = new ManualResetEvent(false);
            int errCount = 0;
            for (int i = 0; i < _allGateways.Length; i++)
            {
                var addr = _allGateways[i];
                Task.Run(() =>
                {
                    try
                    {
                        using (var client = new ProxyClient(this.ProxyAddress, addr, GatewayClientCertificate))
                        {
                            client.ReadTimeout = this.Timeout;
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
                        if (errCount == _allGateways.Length)
                            waitobj.Set();
                    }
                });
            }
            waitobj.WaitOne();
            waitobj.Dispose();

            if (masterAddress == null)
                throw new MissMasterGatewayException("无法找到主网关");
            GatewayAddress = masterAddress;

            if(HistoryMasterAddressList.Any(m=>m== GatewayAddress) == false)
                HistoryMasterAddressList.Add(GatewayAddress);
        }

        /// <summary>
        /// 获取指定微服务
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arg">用于执行微服务自定义客户端检验代码时，传进去的arg变量</param>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual T GetMicroService<T>(string arg = null , RegisterServiceLocation registerServiceLocation = null) where T : IImplInvoker
        {
            var classType = typeof(T);
            var att = classType.GetCustomAttribute<InvokerInfoAttribute>();
            if (att == null)
                throw new Exception($"{classType}不是有效的微服务类型");

            for (int i = 0; i < 2; i ++)
            {
                try
                {
                    var invoker = new Invoker(this, att.ServiceName , arg);
                    if (invoker.Init(registerServiceLocation))
                        return (T)Activator.CreateInstance(classType, new object[] { invoker });
                }
                catch (MissMasterGatewayException)
                {
                    if (i == 1)
                        throw;
                    else
                    {
                        if (GatewayAddress != null)
                            HistoryMasterAddressList.Remove(HistoryMasterAddressList.FirstOrDefault(m=>m == GatewayAddress));
                    }
                    findMasterGateway();
                }
            }
            throw new MissServiceException($"找不到微服务“{att.ServiceName}”");
        }

        /// <summary>
        /// 获取指定微服务
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="arg">用于执行微服务自定义客户端检验代码时，传进去的arg变量</param>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual IMicroService GetMicroService( string serviceName,string arg = null , RegisterServiceLocation registerServiceLocation = null)
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var invoker = new Invoker(this, serviceName, arg);
                    if (invoker.Init(registerServiceLocation))
                        return invoker;
                }
                catch (MissMasterGatewayException)
                {
                    if (i == 1)
                        throw;
                    else
                    {
                        if (GatewayAddress != null)
                            HistoryMasterAddressList.Remove(HistoryMasterAddressList.FirstOrDefault(m => m == GatewayAddress));
                    }
                    findMasterGateway();
                }
            }

            throw new MissServiceException($"找不到微服务“{serviceName}”");
        }
              


        void IRemoteClient.AddConnect(InvokeConnect connect)
        {
            lock(_Connects)
            {
                _Connects.Add(connect);
            }           
        }
        void IRemoteClient.AddTask(Task task)
        {
            lock (_Tasks)
            {
                _Tasks.Add(task);
            }
        }

        void waitTasks()
        {
            try
            {
                Exception lastErr = null;
                for(int i = 0; i < _Tasks.Count; i ++)
                {
                    try
                    {
                        _Tasks[i].Wait();
                    }
                    catch (Exception ex)
                    {
                        lastErr = ex;
                    }
                }
               
                if (lastErr != null)
                    throw lastErr;
            }
            catch (Exception ex)
            {
                var err = ex;
                if(err.InnerException != null)
                {
                    err = err.InnerException;
                }

                throw err;
            }
            finally
            {
                _Tasks.Clear();
            }
           
        }

        /// <summary>
        /// 启动分布式事务
        /// </summary>
        public void BeginTransaction()
        {
            _SupportTransaction = true;
        }

        /// <summary>
        /// 提交分布式事务 (请先使用BeginTransaction启动事务)
        /// </summary>
        public void CommitTransaction()
        {
            var errors = endRequest(InvokeType.CommitTranaction);

            if (errors != null && errors.Count > 0)
                throw new TransactionArrayException(errors, $"有{errors.Count}个服务提交事务失败");

            _SupportTransaction = false;
        }

        List<TransactionException> endRequest(InvokeType invokeType)
        {
            waitTasks();

            if (_Connects.Count > 0)
            {
                List<TransactionException> errors = new List<TransactionException>(_Connects.Count);
                //健康检查
                Parallel.For(0, _Connects.Count, (i) =>
                {
                    var connect = _Connects[i];
                    try
                    {
                        connect.NetClient.WriteServiceData(new InvokeCommand()
                        {
                            Type = InvokeType.HealthyCheck,
                        });
                        var ret = connect.NetClient.ReadServiceObject<InvokeResult>();
                        if (ret.Success == false)
                        {
                            //有人不同意提交事务
                            //把提交更改为回滚
                            invokeType = InvokeType.RollbackTranaction;
                        }
                    }
                    catch (Exception ex)
                    {
                        connect.NetClient.Dispose();
                        connect.NetClient = null;
                        errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
                    }

                });

                if (errors.Count > 0)
                {
                    foreach (var connect in _Connects)
                    {
                        try
                        {
                            connect.NetClient.WriteServiceData(new InvokeCommand()
                            {
                                Type = InvokeType.RollbackTranaction,
                                Header = this.GetCommandHeader()
                            });
                            var ret = connect.NetClient.ReadServiceObject<InvokeResult>();
                        }
                        catch
                        {
                        }
                        NetClientPool.AddClientToPool(connect.NetClient);
                    }
                    if (invokeType == InvokeType.CommitTranaction)
                        throw new TransactionException(null, "提交事务时，有连接中断，所有事务将回滚");
                    else
                        throw new TransactionException(null, "回滚事务时，有连接中断，所有事务将稍后回滚");
                }

                if (errors.Count == 0)
                {
                    Parallel.For(0, _Connects.Count, (i) =>
                    {
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

                                connect.NetClient.WriteServiceData(new InvokeCommand()
                                {
                                    Type = invokeType,
                                    Header = this.GetCommandHeader()
                                });
                                var ret = connect.NetClient.ReadServiceObject<InvokeResult>();
                                if (ret.Success == false)
                                {
                                    errors.Add(new TransactionException(connect.InvokingInfo, ret.Error));
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

                        NetClientPool.AddClientToPool(connect.NetClient);
                    });

                    if (errors.Count > 0)
                    {
                        var successed = _Connects.Where(m => errors.Any(e => e.InvokingInfo == m.InvokingInfo) == false).ToArray();

                        if (successed.Length > 0)
                            _logger?.LogError($"事务:{TransactionId}已经成功{(invokeType == InvokeType.CommitTranaction ? "提交" : "回滚")}，详细请求信息：${successed.ToJsonString()}");
                        foreach (var err in errors)
                            _logger?.LogError(err, $"事务:{TransactionId}发生错误。");
                    }
                }


                _Connects.Clear();
                return errors;
            }
            else
            {
                return null;
            }           
           
        }
        /// <summary>
        /// 回滚分布式事务
        /// </summary>
        public void RollbackTransaction()
        {                
            var errors = endRequest(InvokeType.RollbackTranaction);
            if (errors != null && errors.Count > 0)
                throw new TransactionArrayException(errors, "rollback transaction error");

            _SupportTransaction = false;
        }

        public void Dispose()
        {
            RollbackTransaction();
        }
    }
}
