using JMS.Dtos;
using JMS.GatewayConnection;
using JMS.InvokeConnects;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

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
                _serviceLocation = registerServiceLocation.ToJsonString().FromJson<ClientServiceDetail>();
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
                _serviceLocation = registerServiceLocation.ToJsonString().FromJson<ClientServiceDetail>();
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
            InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this).Invoke<object>(method, RemoteClient, parameters);
        }
        public T Invoke<T>(string method, params object[] parameters)
        {
            return InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this).Invoke<T>(method, RemoteClient, parameters);
        }

        async Task<T> CreateInvokeTask<T>(IInvokeConnect connect,int invokingId, string method, object[] parameters)
        {
          
            if (RemoteClient.SupportTransaction)
            {
                await RemoteClient.WaitConnectComplete(invokingId , _serviceLocation);
            }

            return await connect.InvokeAsync<T>(method, RemoteClient, parameters);
        }

        public Task<T> InvokeAsync<T>(string method, params object[] parameters)
        {
            var id = Interlocked.Increment(ref RemoteClient.InvokingId);
            var connect = InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this);
          
            var task = CreateInvokeTask<T>(connect, id , method, parameters);
            RemoteClient.AddTask(connect, id, task);
            return task;
        }
        public Task InvokeAsync(string method, params object[] parameters)
        {
            var id = Interlocked.Increment(ref RemoteClient.InvokingId);
            var connect = InvokeConnectFactory.Create(RemoteClient, _serviceName, _serviceLocation, this);

            var task = CreateInvokeTask<object>(connect, id, method, parameters);
            RemoteClient.AddTask(connect, id,task);
            return task;
        }

        public string GetServiceClassCode(string nameSpace, string className)
        {
            using (var netclient = new ProxyClient(RemoteClient.ProxyAddress))
            {
                netclient.Connect(new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, RemoteClient.ServiceClientCertificate));
                netclient.ReadTimeout = this.RemoteClient.Timeout;
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = (int)InvokeType.GenerateInvokeCode,
                    Service = _serviceName,
                    Parameters = new string[] { nameSpace, className }
                });
                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                if (!ret.Success)
                    throw new RemoteException(null, ret.Data);
                return ret.Data;
            }
        }

        public string GetServiceInfo()
        {
            using (var netclient = new ProxyClient(RemoteClient.ProxyAddress))
            {
                netclient.Connect(new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, RemoteClient.ServiceClientCertificate));
                netclient.ReadTimeout = this.RemoteClient.Timeout;
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = (int)InvokeType.GenerateServiceInfo,
                    Service = _serviceName
                });
                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                if (!ret.Success)
                    throw new RemoteException(null, ret.Data);
                return ret.Data;
            }
        }
    }
}
