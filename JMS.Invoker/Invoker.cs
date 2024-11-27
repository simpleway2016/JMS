using JMS.Dtos;
using JMS.GatewayConnection;
using JMS.InvokeConnects;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS
{
    class Invoker : IMicroService
    {
        IMicroServiceProvider _microServiceProvider;
        public RemoteClient RemoteClient { get; }
        string _serviceName;
        ClientServiceDetail _serviceLocation;
        public ClientServiceDetail ServiceLocation => _serviceLocation;
        bool _IsFromGateway;
        public bool IsFromGateway => _IsFromGateway;

        public Invoker(RemoteClient remoteClient, IMicroServiceProvider microServiceProvider, string serviceName)
        {
            this._microServiceProvider = microServiceProvider;
            this.RemoteClient = remoteClient;
            _serviceName = serviceName;
            _IsFromGateway = true;
        }

        public bool Init(ClientServiceDetail registerServiceLocation)
        {
            if (registerServiceLocation != null)
            {
                _IsFromGateway = false;
                _serviceLocation = registerServiceLocation.ChangeType<ClientServiceDetail>();
                return true;
            }
            //获取服务地址
            _serviceLocation = _microServiceProvider.GetServiceLocation(RemoteClient, _serviceName);


            return _serviceLocation != null;
        }

        public async Task<bool> InitAsync(ClientServiceDetail registerServiceLocation)
        {
            if (registerServiceLocation != null)
            {
                _IsFromGateway = false;
                _serviceLocation = registerServiceLocation.ChangeType<ClientServiceDetail>();
                return true;
            }
            //获取服务地址
            _serviceLocation = await _microServiceProvider.GetServiceLocationAsync(RemoteClient, _serviceName);


            return _serviceLocation != null;
        }

        public bool Init()
        {
            return Init(null);
        }

        public void Invoke(string method, params object[] parameters)
        {
            var connect = InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this);

            if (RemoteClient.SupportTransaction && RemoteClient.HasTransactionTask())
            {
                var invokingId = Interlocked.Increment(ref RemoteClient.InvokingId);
                RemoteClient.WaitConnectComplete(invokingId, connect).Wait();
            }
            connect.Invoke<object>(method, RemoteClient, parameters);
        }
        public T Invoke<T>(string method, params object[] parameters)
        {
            var connect = InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this);
            if (RemoteClient.SupportTransaction && RemoteClient.HasTransactionTask())
            {
                var invokingId = Interlocked.Increment(ref RemoteClient.InvokingId);
                RemoteClient.WaitConnectComplete(invokingId, connect).Wait();
            }
            return connect.Invoke<T>(method, RemoteClient, parameters);
        }

        async Task<T> CreateInvokeTask<T>(IInvokeConnect connect, int invokingId, string method, object[] parameters)
        {

            if (RemoteClient.SupportTransaction)
            {
                await RemoteClient.WaitConnectComplete(invokingId, connect);
            }

            return await connect.InvokeAsync<T>(method, RemoteClient, parameters);
        }

        async Task<InvokeResult<T>> CreateInvokeExTask<T>(IInvokeConnect connect, int invokingId, string method, object[] parameters)
        {

            if (RemoteClient.SupportTransaction)
            {
                await RemoteClient.WaitConnectComplete(invokingId, connect);
            }

            return await connect.InvokeExAsync<T>(method, RemoteClient, parameters);
        }

        public Task<T> InvokeAsync<T>(string method, params object[] parameters)
        {
            var id = Interlocked.Increment(ref RemoteClient.InvokingId);
            var connect = InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this);

            var task = CreateInvokeTask<T>(connect, id, method, parameters);
            RemoteClient.AddTask(connect, id, task);
            return task;
        }

        public Task<InvokeResult<T>> InvokeExAsync<T>(string method, params object[] parameters)
        {
            var id = Interlocked.Increment(ref RemoteClient.InvokingId);
            var connect = InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this);

            var task = CreateInvokeExTask<T>(connect, id, method, parameters);
            RemoteClient.AddTask(connect, id, task);
            return task;
        }
        public Task InvokeAsync(string method, params object[] parameters)
        {
            var id = Interlocked.Increment(ref RemoteClient.InvokingId);
            var connect = InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this);

            var task = CreateInvokeTask<object>(connect, id, method, parameters);
            RemoteClient.AddTask(connect, id, task);
            return task;
        }

        public string GetServiceClassCode(string nameSpace, string className)
        {
            var netclient = NetClientPool.CreateClient(RemoteClient.ProxyAddress, new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, RemoteClient.ServiceClientCertificate));
            netclient.ReadTimeout = this.RemoteClient.Timeout;
            netclient.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.GenerateInvokeCode,
                Service = _serviceName,
                Parameters = new string[] { nameSpace, className }
            });
            var ret = netclient.ReadServiceObject<InvokeResult<string>>();

            NetClientPool.AddClientToPool(netclient);

            if (!ret.Success)
                throw new RemoteException(null, null, ret.Data);
            return ret.Data;
        }

        public async Task<string> GetServiceClassCodeAsync(string nameSpace, string className)
        {
            var netclient = await NetClientPool.CreateClientAsync(RemoteClient.ProxyAddress, new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, RemoteClient.ServiceClientCertificate));

            netclient.ReadTimeout = this.RemoteClient.Timeout;
            netclient.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.GenerateInvokeCode,
                Service = _serviceName,
                Parameters = new string[] { nameSpace, className }
            });
            var ret = await netclient.ReadServiceObjectAsync<InvokeResult<string>>();

            NetClientPool.AddClientToPool(netclient);

            if (!ret.Success)
                throw new RemoteException(null, null, ret.Data);
            return ret.Data;

        }

        public string GetServiceInfo()
        {
            var netclient = NetClientPool.CreateClient(RemoteClient.ProxyAddress, new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, RemoteClient.ServiceClientCertificate));

            netclient.ReadTimeout = this.RemoteClient.Timeout;
            netclient.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.GenerateServiceInfo,
                Service = _serviceName
            });
            var ret = netclient.ReadServiceObject<InvokeResult<string>>();

            NetClientPool.AddClientToPool(netclient);

            if (!ret.Success)
                throw new RemoteException(null, ret.GetStatusCode(), ret.Data);
            return ret.Data;
        }

        public async Task<string> GetServiceInfoAsync()
        {
            var netclient = await NetClientPool.CreateClientAsync(RemoteClient.ProxyAddress, new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, RemoteClient.ServiceClientCertificate));
            netclient.ReadTimeout = this.RemoteClient.Timeout;
            netclient.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.GenerateServiceInfo,
                Service = _serviceName
            });
            var ret = await netclient.ReadServiceObjectAsync<InvokeResult<string>>();

            NetClientPool.AddClientToPool(netclient);

            if (!ret.Success)
                throw new RemoteException(null, null, ret.Data);
            return ret.Data;
        }
    }
}
