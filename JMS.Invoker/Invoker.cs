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
        IRemoteClient _serviceTransaction;
        IMicroServiceProvider _microServiceProvider;
        public IRemoteClient ServiceTransaction { get; }
        string _serviceName;
        ClientServiceDetail _serviceLocation;
        public ClientServiceDetail ServiceLocation => _serviceLocation;
        bool _IsFromGateway;
        public bool IsFromGateway => _IsFromGateway;

        public Invoker(IRemoteClient serviceTransaction, IMicroServiceProvider microServiceProvider, string serviceName)
        {
            this._serviceTransaction = serviceTransaction;
            this._microServiceProvider = microServiceProvider;
            this.ServiceTransaction = serviceTransaction;
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
            _serviceLocation = _microServiceProvider.GetServiceLocation(ServiceTransaction, _serviceName);


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
            _serviceLocation = await _microServiceProvider.GetServiceLocationAsync(ServiceTransaction, _serviceName);


            return _serviceLocation != null;
        }

        public bool Init()
        {
            return Init(null);
        }

        public void Invoke(string method, params object[] parameters)
        {
            InvokeConnectFactory.Create(ServiceTransaction, _serviceName, _serviceLocation, this).Invoke<object>(method, ServiceTransaction, parameters);
        }
        public T Invoke<T>(string method, params object[] parameters)
        {
            return InvokeConnectFactory.Create(ServiceTransaction, _serviceName, _serviceLocation, this).Invoke<T>(method, ServiceTransaction, parameters);
        }

        async Task<T> CreateInvokeTask<T>(IInvokeConnect connect,int invokingId, string method, object[] parameters)
        {
          
            if (ServiceTransaction.SupportTransaction)
            {
                await ((RemoteClient)ServiceTransaction).WaitConnectComplete(invokingId , _serviceLocation);
            }

            return await connect.InvokeAsync<T>(method, ServiceTransaction, parameters);
        }

        static int InvokingId = 0;
        public Task<T> InvokeAsync<T>(string method, params object[] parameters)
        {
            var id = Interlocked.Increment(ref InvokingId);
            var connect = InvokeConnectFactory.Create(ServiceTransaction, _serviceName, _serviceLocation, this);
          
            var task = CreateInvokeTask<T>(connect, id , method, parameters);
            ServiceTransaction.AddTask(connect, id, task);
            return task;
        }
        public Task InvokeAsync(string method, params object[] parameters)
        {
            var id = Interlocked.Increment(ref InvokingId);
            var connect = InvokeConnectFactory.Create(ServiceTransaction, _serviceName, _serviceLocation, this);

            var task = CreateInvokeTask<object>(connect, id, method, parameters);
            ServiceTransaction.AddTask(connect, id,task);
            return task;
        }

        public string GetServiceClassCode(string nameSpace, string className)
        {
            using (var netclient = new ProxyClient(ServiceTransaction.ProxyAddress))
            {
                netclient.Connect(new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, ServiceTransaction.ServiceClientCertificate));
                netclient.ReadTimeout = this.ServiceTransaction.Timeout;
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
            using (var netclient = new ProxyClient(ServiceTransaction.ProxyAddress))
            {
                netclient.Connect(new NetAddress(_serviceLocation.ServiceAddress, _serviceLocation.Port, ServiceTransaction.ServiceClientCertificate));
                netclient.ReadTimeout = this.ServiceTransaction.Timeout;
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
