using JMS.Dtos;
using JMS.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Impls
{
    internal class RegisterServiceManager : IRegisterServiceManager
    {
        public event EventHandler<RegisterServiceInfo> ServiceConnect;
        public event EventHandler<RegisterServiceInfo> ServiceDisconnect;

        ConcurrentDictionary<string, IMicroServiceReception> _allServiceReceptions = new ConcurrentDictionary<string, IMicroServiceReception>();



        public void AddRegisterService(IMicroServiceReception reception)
        {
            _allServiceReceptions[reception.ServiceInfo.ServiceId] = reception;

            if (ServiceConnect != null)
            {
                ServiceConnect(this, reception.ServiceInfo);
            }
        }

        public IEnumerable<RegisterServiceInfo> GetAllRegisterServices()
        {
            return _allServiceReceptions.Values.Select(m=>m.ServiceInfo);
        }

        public RegisterServiceInfo GetServiceById(string id)
        {
            if(_allServiceReceptions.TryGetValue(id ,out IMicroServiceReception o))
            {
                return o.ServiceInfo;
            }
            return null;
        }

        public void RemoveRegisterService(RegisterServiceInfo registerServiceInfo)
        {
            if (_allServiceReceptions.TryRemove(registerServiceInfo.ServiceId, out IMicroServiceReception o))
            {
                if (ServiceDisconnect != null)
                {
                    ServiceDisconnect(this, o.ServiceInfo);
                }
            }
        }

        public void DisconnectAllServices()
        {
            foreach( var item in _allServiceReceptions )
            {
                item.Value.Close();
            }
        }

        public void DisconnectService(string id)
        {
            if (_allServiceReceptions.TryRemove(id, out IMicroServiceReception o))
            {
                o.Close();
                if (ServiceDisconnect != null)
                {
                    ServiceDisconnect(this, o.ServiceInfo);
                }
            }
        }
    }
}
