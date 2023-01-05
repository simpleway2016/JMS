using JMS.Dtos;
using JMS.InvokeConnects;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    class Invoker: IMicroService
    {
        public IRemoteClient ServiceTransaction { get; }
        string _serviceName;
        ClientServiceDetail _serviceLocation;
        public ClientServiceDetail ServiceLocation => _serviceLocation;
        bool _IsFromGateway;
        public bool IsFromGateway => _IsFromGateway;

        public Invoker(IRemoteClient ServiceTransaction, string serviceName)
        {
            this.ServiceTransaction = ServiceTransaction;
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
            var netclient = NetClientPool.CreateClient(this.ServiceTransaction.ProxyAddress,this.ServiceTransaction.GatewayAddress);
            netclient.ReadTimeout = this.ServiceTransaction.Timeout;
            try
            {
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetServiceProvider,
                    Header = ServiceTransaction.GetCommandHeader(),
                    Content = new GetServiceProviderRequest
                    {
                        ServiceName = _serviceName
                    }.ToJsonString()
                });
                var serviceLocation = netclient.ReadServiceObject<ClientServiceDetail>();

                if (serviceLocation.ServiceAddress == "not master")
                    throw new MissMasterGatewayException("");

                if(serviceLocation.Port == 0 && string.IsNullOrEmpty(serviceLocation.ServiceAddress))
                {
                    //网关没有这个服务
                    return false;
                }

                _serviceLocation = serviceLocation;

                NetClientPool.AddClientToPool(netclient);
            }
            catch (SocketException ex)
            {
                netclient.Dispose();
                throw new MissMasterGatewayException(ex.Message);
            }
            catch (Exception)
            {
                netclient.Dispose();
                throw;
            }


            return true;
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
            var netclient = await NetClientPool.CreateClientAsync(this.ServiceTransaction.ProxyAddress, this.ServiceTransaction.GatewayAddress);
            netclient.ReadTimeout = this.ServiceTransaction.Timeout;
            try
            {
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetServiceProvider,
                    Header = ServiceTransaction.GetCommandHeader(),
                    Content = new GetServiceProviderRequest
                    {
                        ServiceName = _serviceName
                    }.ToJsonString()
                });
                var serviceLocation = await netclient.ReadServiceObjectAsync<ClientServiceDetail>();

                if (serviceLocation.ServiceAddress == "not master")
                    throw new MissMasterGatewayException("");

                if (serviceLocation.Port == 0 && string.IsNullOrEmpty(serviceLocation.ServiceAddress))
                {
                    //网关没有这个服务
                    return false;
                }

                _serviceLocation = serviceLocation;

                NetClientPool.AddClientToPool(netclient);
            }
            catch (SocketException ex)
            {
                netclient.Dispose();
                throw new MissMasterGatewayException(ex.Message);
            }
            catch (Exception)
            {
                netclient.Dispose();
                throw;
            }


            return true;
        }

        public bool Init()
        {
            return Init(null);
        }

        public void Invoke(string method, params object[] parameters)
        {
            InvokeConnectFactory.Create(_serviceName, _serviceLocation,this).Invoke<object>(method, ServiceTransaction, parameters);
        }
        public T Invoke<T>(string method, params object[] parameters)
        {
            return InvokeConnectFactory.Create(_serviceName, _serviceLocation, this).Invoke<T>(method, ServiceTransaction, parameters);
        }
        public Task<T> InvokeAsync<T>(string method, params object[] parameters)
        {
            var task = InvokeConnectFactory.Create(_serviceName, _serviceLocation, this).InvokeAsync<T>(method,  ServiceTransaction, parameters);
            ServiceTransaction.AddTask(task);
            return task;
        }
        public Task InvokeAsync(string method, params object[] parameters)
        {
            var task = InvokeConnectFactory.Create(_serviceName, _serviceLocation, this).InvokeAsync<object>(method, ServiceTransaction, parameters);
            ServiceTransaction.AddTask(task);
            return task;
        }

        public string GetServiceClassCode(string nameSpace, string className)
        {
            using (var netclient = new ProxyClient(ServiceTransaction.ProxyAddress))
            {
                netclient.Connect(new NetAddress( _serviceLocation.ServiceAddress, _serviceLocation.Port, ServiceTransaction.ServiceClientCertificate));
                netclient.ReadTimeout = this.ServiceTransaction.Timeout;
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = InvokeType.GenerateInvokeCode,
                    Service = _serviceName,
                    Parameters = new string[] { nameSpace, className }
                });
                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                if (!ret.Success)
                    throw new RemoteException(null,ret.Data);
                return ret.Data;
            }
        }

        public string GetServiceInfo()
        {
            using (var netclient = new ProxyClient(ServiceTransaction.ProxyAddress))
            {
                netclient.Connect(new NetAddress( _serviceLocation.ServiceAddress, _serviceLocation.Port, ServiceTransaction.ServiceClientCertificate));
                netclient.ReadTimeout = this.ServiceTransaction.Timeout;
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = InvokeType.GenerateServiceInfo,
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
