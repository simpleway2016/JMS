using JMS.Dtos;
using JMS.GatewayConnection;
using JMS.TransactionReporters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        internal List<IInvokeConnect> _Connects = new List<IInvokeConnect>();
        TaskCollection _transactionTasks = new TaskCollection();
        TaskCollection _normalTasks = new TaskCollection();

        private string _TransactionId;
        public string TransactionId
        {
            get => _TransactionId;
        }

        internal string TransactionFlag;
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

        public NetAddress GatewayAddress { get; private set; }
        public NetAddress ProxyAddress { get; }
        NetAddress[] _allGateways;

        Dictionary<string, string> _Header = new Dictionary<string, string>();
        public X509Certificate2 ServiceClientCertificate { get;  set; }
        MasterGatewayProvider _masterGatewayProvider;
        ILogger<RemoteClient> _logger;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gatewayAddress">网关ip</param>
        /// <param name="port">网关端口</param>
        /// <param name="proxyAddress"></param>
        /// <param name="logger">日志对象，用于在事务发生意外时，记录详细信息</param>
        /// <param name="serviceClientCert">与微服务互通的证书</param>
        public RemoteClient(string gatewayAddress, int port, NetAddress proxyAddress = null, ILogger<RemoteClient> logger = null,X509Certificate2 serviceClientCert = null)
        {
            _TransactionId = Guid.NewGuid().ToString("N");
            GatewayAddress = new NetAddress(gatewayAddress, port);
            ServiceClientCertificate = serviceClientCert;
            this.ProxyAddress = proxyAddress;
            TransactionReporterRoute.Logger = _logger = logger;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gatewayAddresses">多个集群网关地址</param>
        /// <param name="proxyAddress"></param>
        /// <param name="logger">日志对象，用于在事务发生意外时，记录详细信息</param>
        /// <param name="serviceClientCert">与微服务互通的证书</param>
        public RemoteClient(NetAddress[] gatewayAddresses,NetAddress proxyAddress = null, ILogger<RemoteClient> logger = null, X509Certificate2 serviceClientCert = null)
        {
            _TransactionId = Guid.NewGuid().ToString("N");
            TransactionReporterRoute.Logger = _logger = logger;
            this.ProxyAddress = proxyAddress;
            ServiceClientCertificate = serviceClientCert;

            //先找到master网关
            _allGateways = gatewayAddresses;
        }

        /// <summary>
        /// 获取当前微服务列表
        /// </summary>
        /// <param name="serviceName">服务名称，空表示所有服务</param>
        /// <returns></returns>
        public RegisterServiceRunningInfo[] ListMicroService(string serviceName)
        {
            if(GatewayAddress == null)
            {
                findMasterGateway();
            }
            var client = NetClientPool.CreateClient(this.ProxyAddress, GatewayAddress);

            try
            {
                client.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetAllServiceProviders,
                    Content = serviceName,
                    Header = this.GetCommandHeader(),
                });
                var serviceLocations = client.ReadServiceObject<RegisterServiceRunningInfo[]>();
                NetClientPool.AddClientToPool(client);
                return serviceLocations;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 获取当前微服务列表
        /// </summary>
        /// <param name="serviceName">服务名称，空表示所有服务</param>
        /// <returns></returns>
        public async Task<RegisterServiceRunningInfo[]> ListMicroServiceAsync(string serviceName)
        {
            if (GatewayAddress == null)
            {
                await findMasterGatewayAsync();
            }
            var client = NetClientPool.CreateClient(this.ProxyAddress, GatewayAddress);
            try
            {
                client.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetAllServiceProviders,
                    Content = serviceName,
                    Header = this.GetCommandHeader(),
                });
                var serviceLocations = await client.ReadServiceObjectAsync<RegisterServiceRunningInfo[]>();
                NetClientPool.AddClientToPool(client);
                return serviceLocations;
            }
            catch
            {
                client.Dispose();
                throw;
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

            if (GatewayAddress == null)
            {
                findMasterGateway();
            }

            var infos = ListMicroService(serviceName);
            var ret = new RegisterServiceLocation[infos.Length];
            for(int i = 0; i < infos.Length; i ++)
            {
                var info = infos[i];
                ret[i] = new RegisterServiceLocation { 
                    Host = info.Host,
                    Port = info.Port,
                    ServiceAddress = info.ServiceAddress
                };
            }
            return ret;
        }

        /// <summary>
        /// 获取指定某个微服务的地址列表
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns></returns>
        public async Task<RegisterServiceLocation[]> ListMicroServiceLocationsAsync(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new Exception("serviceName is empty");

            if (GatewayAddress == null)
            {
                await findMasterGatewayAsync();
            }

            var infos = await ListMicroServiceAsync(serviceName);
            var ret = new RegisterServiceLocation[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                ret[i] = new RegisterServiceLocation
                {
                    Host = info.Host,
                    Port = info.Port,
                    ServiceAddress = info.ServiceAddress
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
            serviceAddress.Certificate = ServiceClientCertificate;
            using (var netclient = new ProxyClient(this.ProxyAddress))
            {
                netclient.Connect(serviceAddress);
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
            serviceAddress.Certificate = ServiceClientCertificate;
            using (var netclient = new ProxyClient(this.ProxyAddress))
            {
                netclient.Connect(serviceAddress);
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = InvokeType.GetAllLockedKeys,
                });
                var ret = netclient.ReadServiceObject<InvokeResult<string[]>>();
                return ret.Data;
            }
        }

        public bool TryGetHeader(string key,out string value)
        {
            return _Header.TryGetValue(key, out value);
        }

        public void SetHeader(string key,string value)
        {
            if (key == "TranId")
                throw new Exception("key='TranId' is not allow");
            else if (key == "Tran")
                throw new Exception("key='Tran' is not allow");
            else if (key == "TranFlag")
                throw new Exception("key='TranFlag' is not allow");
            _Header[key] = value;
        }

        public Dictionary<string,string> GetCommandHeader()
        {
            var header = new Dictionary<string, string>();
            header["TranId"] = this.TransactionId;

            if (_SupportTransaction == false)
                header["Tran"] = "0";
            else
            {
                header["TranFlag"] = TransactionFlag;
            }

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
            if (_masterGatewayProvider == null)
            {
                _masterGatewayProvider = MasterGatewayProvider.Create(this.ProxyAddress, _allGateways, this.Timeout);
            }
            GatewayAddress = _masterGatewayProvider.GetMaster();
        }

        async Task findMasterGatewayAsync()
        {
            if (_allGateways == null || _allGateways.Length == 1)
            {
                if (_allGateways != null)
                    GatewayAddress = _allGateways[0];
                return;
            }
            if (_masterGatewayProvider == null)
            {
                _masterGatewayProvider = MasterGatewayProvider.Create(this.ProxyAddress, _allGateways, this.Timeout);
            }
            GatewayAddress = await _masterGatewayProvider.GetMasterAsync();
        }

        /// <summary>
        /// 获取指定微服务，获取不到微服务则抛出异常
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual T GetMicroService<T>( ClientServiceDetail registerServiceLocation = null) where T : IImplInvoker
        {
            var ret = TryGetMicroService<T>( registerServiceLocation);
            if (ret == null)
            {
                var classType = typeof(T);
                var att = classType.GetCustomAttribute<InvokerInfoAttribute>();
                throw new MissServiceException($"找不到微服务“{att.ServiceName}”");
            }
            return ret;
        }

        /// <summary>
        /// 获取指定微服务，获取不到微服务则抛出异常
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual async Task<T> GetMicroServiceAsync<T>(ClientServiceDetail registerServiceLocation = null) where T : IImplInvoker
        {
            var ret = await TryGetMicroServiceAsync<T>(registerServiceLocation);
            if (ret == null)
            {
                var classType = typeof(T);
                var att = classType.GetCustomAttribute<InvokerInfoAttribute>();
                throw new MissServiceException($"找不到微服务“{att.ServiceName}”");
            }
            return ret;
        }

        /// <summary>
        /// 获取指定微服务, 获取不到微服务则返回null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual T TryGetMicroService<T>( ClientServiceDetail registerServiceLocation = null) where T : IImplInvoker
        {
            if (GatewayAddress == null)
            {
                findMasterGateway();
            }

            var classType = typeof(T);
            var att = classType.GetCustomAttribute<InvokerInfoAttribute>();
            if (att == null)
                throw new Exception($"{classType}不是有效的微服务类型");

            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var invoker = new Invoker(this, att.ServiceName);
                    if (invoker.Init(registerServiceLocation))
                        return (T)Activator.CreateInstance(classType, new object[] { invoker });
                }
                catch (MissMasterGatewayException)
                {
                    if (i == 1)
                        throw;
                    else
                    {
                        _masterGatewayProvider.RemoveMaster();
                    }
                    findMasterGateway();
                }
            }
            return default(T);
        }

        /// <summary>
        /// 获取指定微服务, 获取不到微服务则返回null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual async Task<T> TryGetMicroServiceAsync<T>(ClientServiceDetail registerServiceLocation = null) where T : IImplInvoker
        {
            if (GatewayAddress == null)
            {
                await findMasterGatewayAsync();
            }

            var classType = typeof(T);
            var att = classType.GetCustomAttribute<InvokerInfoAttribute>();
            if (att == null)
                throw new Exception($"{classType}不是有效的微服务类型");

            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var invoker = new Invoker(this, att.ServiceName);
                    if (await invoker.InitAsync(registerServiceLocation))
                        return (T)Activator.CreateInstance(classType, new object[] { invoker });
                }
                catch (MissMasterGatewayException)
                {
                    if (i == 1)
                        throw;
                    else
                    {
                        _masterGatewayProvider.RemoveMaster();
                    }
                    await findMasterGatewayAsync();
                }
            }
            return default(T);
        }

        /// <summary>
        /// 获取指定微服务, 获取不到微服务则抛出异常
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual IMicroService GetMicroService( string serviceName,ClientServiceDetail registerServiceLocation = null)
        {
            var ret = TryGetMicroService(serviceName,registerServiceLocation);
            if(ret == null)
                throw new MissServiceException($"找不到微服务“{serviceName}”");

            return ret;
        }

        /// <summary>
        /// 获取指定微服务, 获取不到微服务则抛出异常
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual async Task<IMicroService> GetMicroServiceAsync(string serviceName, ClientServiceDetail registerServiceLocation = null)
        {
            var ret = await TryGetMicroServiceAsync(serviceName, registerServiceLocation);
            if (ret == null)
                throw new MissServiceException($"找不到微服务“{serviceName}”");

            return ret;
        }

        /// <summary>
        /// 获取指定微服务, 获取不到微服务则返回null
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual IMicroService TryGetMicroService(string serviceName, ClientServiceDetail registerServiceLocation = null)
        {
            if (GatewayAddress == null)
            {
                findMasterGateway();
            }

            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var invoker = new Invoker(this, serviceName);
                    if (invoker.Init(registerServiceLocation))
                        return invoker;
                }
                catch (MissMasterGatewayException)
                {
                    if (i == 1)
                        throw;
                    else
                    {
                        _masterGatewayProvider.RemoveMaster();
                    }
                    findMasterGateway();
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定微服务, 获取不到微服务则返回null
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="registerServiceLocation">指定服务器地址，默认null，表示由网关自动分配</param>
        /// <returns></returns>
        public virtual async Task<IMicroService> TryGetMicroServiceAsync(string serviceName, ClientServiceDetail registerServiceLocation = null)
        {
            if (GatewayAddress == null)
            {
                await findMasterGatewayAsync();
            }
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var invoker = new Invoker(this, serviceName);
                    if (await invoker.InitAsync(registerServiceLocation))
                        return invoker;
                }
                catch (MissMasterGatewayException)
                {
                    if (i == 1)
                        throw;
                    else
                    {
                        _masterGatewayProvider.RemoveMaster();
                    }
                    await findMasterGatewayAsync();
                }
            }

            return null;
        }




        void IRemoteClient.AddConnect(IInvokeConnect connect)
        {
            lock(_Connects)
            {
                _Connects.Add(connect);
            }           
        }
        void IRemoteClient.AddTask(Task task)
        {
            if(_SupportTransaction)
            {
                _transactionTasks.AddTask(task);
            }
            else
            {
                _normalTasks.AddTask(task);
            }
        }

        void waitTasks()
        {
            var errs = _transactionTasks.Wait();
            if ( errs != null && errs.Count > 0)
                throw errs[0];

        }

        async Task waitTasksAsync()
        {
            var errs = await _transactionTasks.WaitAsync();
            if (errs != null && errs.Count > 0)
                throw errs[0];

        }

        /// <summary>
        /// 启动分布式事务
        /// </summary>
        public void BeginTransaction()
        {
            if (!_SupportTransaction)
            {
                this.TransactionFlag = Guid.NewGuid().ToString("N");
                _TransactionId = Guid.NewGuid().ToString("N");
                _SupportTransaction = true;
            }
        }


        /// <summary>
        /// 提交分布式事务 (请先使用BeginTransaction启动事务)
        /// </summary>
        public void CommitTransaction()
        {
            if (_SupportTransaction)
            {
                var errors = endRequest(InvokeType.CommitTranaction);

                if (errors != null && errors.Count > 0)
                    throw new TransactionArrayException(errors, $"有{errors.Count}个服务提交事务失败");

                _SupportTransaction = false;
            }
        }

        /// <summary>
        /// 提交分布式事务 (请先使用BeginTransaction启动事务)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="TransactionArrayException"></exception>
        public async Task CommitTransactionAsync()
        {
            if (_SupportTransaction)
            {
                var errors = await endRequestAsync(InvokeType.CommitTranaction);

                if (errors != null && errors.Count > 0)
                    throw new TransactionArrayException(errors, $"有{errors.Count}个服务提交事务失败");

                _SupportTransaction = false;
            }
        }

        async Task<List<TransactionException>> endRequestAsync(InvokeType invokeType)
        {
            await waitTasksAsync();

            if (_Connects.Count > 0)
            {
                List<TransactionException> errors = new List<TransactionException>(_Connects.Count);
                //健康检查
                if (invokeType == InvokeType.CommitTranaction)
                {
                    for(int i = 0; i < _Connects.Count; i ++)
                    {
                        var connect = _Connects[i];
                        try
                        {
                            var ret = await connect.GoReadyCommitAsync(this);
                            if (ret.Success == false)
                            {
                                //有人不同意提交事务
                                //把提交更改为回滚
                                invokeType = InvokeType.RollbackTranaction;
                            }
                        }
                        catch (Exception ex)
                        {
                            connect.Dispose();
                            _Connects[i] = null;
                            errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
                        }

                    };
                }

                if (errors.Count > 0)
                {
                    foreach (var connect in _Connects)
                    {
                        if (connect == null)
                            continue;

                        try
                        {
                            await connect.GoRollbackAsync(this);
                            connect.AddClientToPool();
                        }
                        catch
                        {
                            connect.Dispose();
                        }
                        
                    }
                    if (invokeType == InvokeType.CommitTranaction)
                        throw new TransactionException(null, "提交事务时，有连接中断，所有事务将回滚");
                    else
                        throw new TransactionException(null, "回滚事务时，有连接中断，所有事务将稍后回滚");
                }

                if (errors.Count == 0)
                {
                    var reporter = TransactionReporterRoute.GetReporter(this);

                    if (invokeType == InvokeType.CommitTranaction)
                    {
                        reporter.ReportTransactionSuccess(this,this.TransactionId);
                    }
                    for(int i = 0; i < _Connects.Count; i ++)
                    {
                        var connect = _Connects[i];
                        if (connect == null)
                            continue;

                        try
                        {
                            var ret = invokeType == InvokeType.CommitTranaction ? await connect.GoCommitAsync(this) : await connect.GoRollbackAsync(this);
                            if (ret.Success == false)
                            {
                                errors.Add(new TransactionException(connect.InvokingInfo, ret.Error));
                            }
                            connect.AddClientToPool();
                        }
                        catch (Exception ex)
                        {
                            connect.Dispose();
                            errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
                        }
                    }

                    if (errors.Count > 0)
                    {
                        var successed = _Connects.Where(m => errors.Any(e => e.InvokingInfo == m.InvokingInfo) == false).ToArray();

                        if (invokeType == InvokeType.CommitTranaction)
                        {
                            if (successed.Length > 0)
                                _logger?.LogError($"事务:{TransactionId}已经成功{(invokeType == InvokeType.CommitTranaction ? "提交" : "回滚")}，详细请求信息：${successed.ToJsonString()}");
                            foreach (var err in errors)
                                _logger?.LogError(err, $"事务:{TransactionId}发生错误。");
                        }
                    }
                    else
                    {
                        if (invokeType == InvokeType.CommitTranaction)
                        {
                            Task.Run(() => {
                                reporter.ReportTransactionCompleted(this,this.TransactionId);
                            });
                        }
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

        List<TransactionException> endRequest(InvokeType invokeType)
        {
            waitTasks();

            if (_Connects.Count > 0)
            {
                List<TransactionException> errors = new List<TransactionException>(_Connects.Count);
                //健康检查
                if (invokeType == InvokeType.CommitTranaction)
                {
                    for(int i = 0; i < _Connects.Count; i ++)
                    {
                        var connect = _Connects[i];
                        try
                        {
                            var ret = connect.GoReadyCommit(this);
                            if (ret.Success == false)
                            {
                                //有人不同意提交事务
                                //把提交更改为回滚
                                invokeType = InvokeType.RollbackTranaction;
                            }
                        }
                        catch (Exception ex)
                        {
                            connect.Dispose();
                            _Connects[i] = null;
                            errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
                        }

                    }
                }

                if (errors.Count > 0)
                {
                    foreach (var connect in _Connects)
                    {
                        if (connect == null)
                            continue;

                        try
                        {
                            connect.GoRollback(this);
                            connect.AddClientToPool();
                        }
                        catch
                        {
                            connect.Dispose();
                        }

                    }
                    if (invokeType == InvokeType.CommitTranaction)
                        throw new TransactionException(null, "提交事务时，有连接中断，所有事务将回滚");
                    else
                        throw new TransactionException(null, "回滚事务时，有连接中断，所有事务将稍后回滚");
                }

                if (errors.Count == 0)
                {
                    var reporter = TransactionReporterRoute.GetReporter(this);

                    if (invokeType == InvokeType.CommitTranaction)
                    {
                        reporter.ReportTransactionSuccess(this, this.TransactionId);
                    }
                    for (int i = 0; i < _Connects.Count; i++)
                    {
                        var connect = _Connects[i];
                        if (connect == null)
                            continue;

                        try
                        {
                            var ret = invokeType == InvokeType.CommitTranaction ? connect.GoCommit(this) : connect.GoRollback(this);
                            if (ret.Success == false)
                            {
                                errors.Add(new TransactionException(connect.InvokingInfo, ret.Error));
                            }
                            connect.AddClientToPool();
                        }
                        catch (Exception ex)
                        {
                            connect.Dispose();
                            errors.Add(new TransactionException(connect.InvokingInfo, ex.Message));
                            
                        }
                    }

                    if (errors.Count > 0)
                    {
                        var successed = _Connects.Where(m => errors.Any(e => e.InvokingInfo == m.InvokingInfo) == false).ToArray();

                        if (invokeType == InvokeType.CommitTranaction)
                        {
                            if (successed.Length > 0)
                                _logger?.LogError($"事务:{TransactionId}已经成功{(invokeType == InvokeType.CommitTranaction ? "提交" : "回滚")}，详细请求信息：${successed.ToJsonString()}");
                            foreach (var err in errors)
                                _logger?.LogError(err, $"事务:{TransactionId}发生错误。");
                        }
                    }
                    else
                    {
                        if (invokeType == InvokeType.CommitTranaction)
                        {
                            Task.Run(() => {
                                reporter.ReportTransactionCompleted(this, this.TransactionId);
                            });
                        }
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
            if (_SupportTransaction)
            {
                var errors = endRequest(InvokeType.RollbackTranaction);
                if (errors != null && errors.Count > 0)
                    throw new TransactionArrayException(errors, "rollback transaction error");

                _SupportTransaction = false;
            }
        }

        /// <summary>
        /// 回滚分布式事务
        /// </summary>
        public async Task RollbackTransactionAsync()
        {
            if (_SupportTransaction)
            {
                var errors = await endRequestAsync(InvokeType.RollbackTranaction);
                if (errors != null && errors.Count > 0)
                    throw new TransactionArrayException(errors, "rollback transaction error");

                _SupportTransaction = false;
            }
        }


        public void Dispose()
        {
            if (_SupportTransaction)
            {
                RollbackTransaction();
            }

            var errs = _normalTasks.Wait();
            if (errs != null && errs.Count > 0)
                throw errs[0];
        }
    }
}
