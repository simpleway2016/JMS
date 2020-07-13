using JMS.Dtos;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    public class Invoker: IMicroService
    {
        public MicroServiceTransaction ServiceTransaction { get; }
        string _serviceName;
        RegisterServiceLocation _serviceLocation;
        public Invoker(MicroServiceTransaction ServiceTransaction, string serviceName)
        {
            this.ServiceTransaction = ServiceTransaction;
            _serviceName = serviceName;


        }
        public bool Init()
        {
            //获取服务地址
            var netclient = NetClientPool.CreateClient(this.ServiceTransaction.ProxyAddress, this.ServiceTransaction.GatewayAddress, this.ServiceTransaction.GatewayClientCertificate);
            try
            {
                netclient.WriteServiceData(new GatewayCommand()
                {
                    Type = CommandType.GetServiceProvider,
                    Header = ServiceTransaction.GetCommandHeader(),
                    Content = new GetServiceProviderRequest
                    {
                        ServiceName = _serviceName,
                    }.ToJsonString()
                });
                var serviceLocation = netclient.ReadServiceObject<RegisterServiceLocation>();

                if (string.IsNullOrEmpty(ServiceTransaction.TransactionId))
                    ServiceTransaction.TransactionId = serviceLocation.TransactionId;

                if (serviceLocation.Port == 0)
                    return false;
                _serviceLocation = serviceLocation;

                NetClientPool.AddClientToPool(netclient);
            }
            catch (Exception)
            {
                netclient.Dispose();
                throw;
            }
               
            
            return true;
        }

        public void Invoke(string method, params object[] parameters)
        {
            new InvokeConnect(_serviceName, _serviceLocation).Invoke<object>(method, ServiceTransaction, parameters);
        }
        public T Invoke<T>(string method, params object[] parameters)
        {
            return new InvokeConnect(_serviceName, _serviceLocation).Invoke<T>(method, ServiceTransaction, parameters);
        }
        public Task<T> InvokeAsync<T>(string method, params object[] parameters)
        {
            var task = new InvokeConnect(_serviceName, _serviceLocation).InvokeAsync<T>(method,  ServiceTransaction, parameters);
            ServiceTransaction.AddTask(task);
            return task;
        }
        public Task InvokeAsync(string method, params object[] parameters)
        {
            var task = new InvokeConnect(_serviceName, _serviceLocation).InvokeAsync<object>(method, ServiceTransaction, parameters);
            ServiceTransaction.AddTask(task);
            return task;
        }

        public string GetServiceClassCode(string nameSpace, string className)
        {
            using (var netclient = new ProxyClient(ServiceTransaction.ProxyAddress ,new Common.Dtos.NetAddress(_serviceLocation.Host, _serviceLocation.Port) , ServiceTransaction.ServiceClientCertificate))
            {
                netclient.WriteServiceData(new InvokeCommand()
                {
                    Type = InvokeType.GenerateInvokeCode,
                    Service = _serviceName,
                    Parameters = new string[] { nameSpace, className }
                });
                var ret = netclient.ReadServiceObject<InvokeResult<string>>();
                if (!ret.Success)
                    throw new RemoteException(ret.Error);
                return ret.Data;
            }
        }
    }
}
