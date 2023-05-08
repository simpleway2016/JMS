using JMS.Applications;
using JMS.Applications.CommandHandles;
using JMS.Common;
using JMS.Dtos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Domains
{
    internal interface IRemoteClientManager
    {
        void AddRemoteClient(NetClient netClient);
        void RemoveRemoteClient(NetClient netClient);
    }

    class DefaultRemoteClientManager : IRemoteClientManager
    {
        ICommandHandlerRoute _commandHandlerRoute;
        IRegisterServiceManager _registerServiceManager;
        ConcurrentDictionary<NetClient, bool> _clients = new ConcurrentDictionary<NetClient, bool>();

        public DefaultRemoteClientManager(IRegisterServiceManager registerServiceManager, ICommandHandlerRoute commandHandlerRoute)
        {
            this._commandHandlerRoute = commandHandlerRoute;
            this._registerServiceManager = registerServiceManager;
            _registerServiceManager.ServiceConnect += _registerServiceManager_ServiceConnect;
            _registerServiceManager.ServiceDisconnect += _registerServiceManager_ServiceDisconnect;
            _registerServiceManager.ServiceInfoRefresh += _registerServiceManager_ServiceInfoRefresh; ;
        }

        private void _registerServiceManager_ServiceInfoRefresh(object sender, RegisterServiceInfo serviceInfo)
        {
            if (!string.IsNullOrWhiteSpace(serviceInfo.ClientCheckCodeFile))
                return;

            var obj = new
            {
                Type = 3,
                Data = new RegisterServiceRunningInfo
                {
                    Host = serviceInfo.Host,
                    ServiceAddress = serviceInfo.ServiceAddress,
                    Port = serviceInfo.Port,
                    ServiceId = serviceInfo.ServiceId,
                    ServiceList = serviceInfo.ServiceList,
                    MaxThread = serviceInfo.MaxThread,
                    UseSsl = serviceInfo.UseSsl,
                    MaxRequestCount = serviceInfo.MaxRequestCount,
                    PerformanceInfo = new PerformanceInfo
                    {
                        RequestQuantity = serviceInfo.RequestQuantity,
                        CpuUsage = serviceInfo.CpuUsage
                    }
                }.ToJsonString()
            };
            foreach (var client in _clients)
            {
                try
                {
                    client.Key.WriteServiceData(obj);
                }
                catch
                {

                }
            }
        }

        private void _registerServiceManager_ServiceDisconnect(object sender, Dtos.RegisterServiceInfo serviceInfo)
        {
            var obj = new
            {
                Type = 2,
                Data = serviceInfo.ServiceId
            };
            foreach (var client in _clients)
            {
                try
                {
                    client.Key.WriteServiceData(obj);
                }
                catch
                {

                }
            }
        }

        private void _registerServiceManager_ServiceConnect(object sender, Dtos.RegisterServiceInfo serviceInfo)
        {
            if (!string.IsNullOrWhiteSpace(serviceInfo.ClientCheckCodeFile))
                return;

            var obj = new
            {
                Type = 1,
                Data = new RegisterServiceRunningInfo
                {
                    Host = serviceInfo.Host,
                    ServiceAddress = serviceInfo.ServiceAddress,
                    Port = serviceInfo.Port,
                    ServiceId = serviceInfo.ServiceId,
                    ServiceList = serviceInfo.ServiceList,
                    MaxThread = serviceInfo.MaxThread,
                    UseSsl = serviceInfo.UseSsl,
                    MaxRequestCount = serviceInfo.MaxRequestCount,
                    PerformanceInfo = new PerformanceInfo
                    {
                        RequestQuantity = serviceInfo.RequestQuantity,
                        CpuUsage = serviceInfo.CpuUsage
                    }
                }.ToJsonString()
            };
            foreach (var client in _clients)
            {
                try
                {
                    client.Key.WriteServiceData(obj);
                }
                catch
                {

                }
            }
        }

        public void AddRemoteClient(NetClient netClient)
        {
            _clients.TryAdd(netClient, true);

            //输出目前所有服务信息
            netClient.WriteServiceData(list());
        }

        RegisterServiceRunningInfo[] list()
        {
            var list = _registerServiceManager.GetAllRegisterServices().Where(m=>string.IsNullOrWhiteSpace(m.ClientCheckCodeFile));

            return list.Select(m => new RegisterServiceRunningInfo
            {
                Host = m.Host,
                ServiceAddress = m.ServiceAddress,
                Port = m.Port,
                ServiceId = m.ServiceId,
                ServiceList = m.ServiceList,
                MaxThread = m.MaxThread,
                UseSsl = m.UseSsl,
                MaxRequestCount = m.MaxRequestCount,
                PerformanceInfo = new PerformanceInfo
                {
                    RequestQuantity = m.RequestQuantity,
                    CpuUsage = m.CpuUsage
                }
            }).ToArray();
        }

        public void RemoveRemoteClient(NetClient netClient)
        {
            _clients.TryRemove(netClient, out bool o);
        }
    }
}
