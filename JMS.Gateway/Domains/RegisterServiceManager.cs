using JMS.Domains;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Domains
{
    internal class RegisterServiceManager : IRegisterServiceManager
    {
        IConfiguration _configuration;
        public event EventHandler<RegisterServiceInfo> ServiceConnect;
        public event EventHandler<RegisterServiceInfo> ServiceInfoRefresh;
        public event EventHandler<RegisterServiceInfo> ServiceDisconnect;

        ConcurrentDictionary<IMicroServiceReception,bool> _allServiceReceptions = new ConcurrentDictionary<IMicroServiceReception, bool>();

        public bool SupportJmsDoc => _configuration.GetSection("Http:SupportJmsDoc").Get<bool>();

        public RegisterServiceManager(IConfiguration configuration)
        {
            this._configuration = configuration;

        }

        public void AddRegisterService(IMicroServiceReception reception)
        {
            _allServiceReceptions[reception] = true;

            if (ServiceConnect != null)
            {
                ServiceConnect(this, reception.ServiceInfo);
            }
        }

        public IEnumerable<RegisterServiceInfo> GetAllRegisterServices()
        {
            return (from m in _allServiceReceptions.Keys
                    select m.ServiceInfo);
        }

        public RegisterServiceInfo GetServiceById(string id)
        {            
            return _allServiceReceptions.Keys.FirstOrDefault(m=>m.ServiceInfo.ServiceId == id)?.ServiceInfo;
        }

        public void RemoveRegisterService(IMicroServiceReception microServiceReception)
        {
            if (_allServiceReceptions.TryRemove(microServiceReception, out bool o))
            {
                if (ServiceDisconnect != null)
                {
                    ServiceDisconnect(this, microServiceReception.ServiceInfo);
                }
            }
        }

        public void DisconnectAllServices()
        {
            foreach( var item in _allServiceReceptions )
            {
                item.Key.Close();
            }
        }

        public void DisconnectService(string id)
        {
            var reception = _allServiceReceptions.Keys.FirstOrDefault(m => m.ServiceInfo.ServiceId == id);
            if (_allServiceReceptions.TryRemove(reception, out bool o))
            {
                reception.Close();
                if (ServiceDisconnect != null)
                {
                    ServiceDisconnect(this, reception.ServiceInfo);
                }
            }
        }

        public void RefreshServiceInfo(RegisterServiceInfo serviceInfo)
        {
            if(ServiceInfoRefresh != null)
            {
                ServiceInfoRefresh(this, serviceInfo);
            }
        }
    }
}
