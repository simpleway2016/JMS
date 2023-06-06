using JMS.Domains;
using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Domains
{
    /// <summary>
    /// 注册服务管理中心
    /// </summary>
    public interface IRegisterServiceManager
    {
        public event EventHandler<RegisterServiceInfo> ServiceConnect;
        public event EventHandler<RegisterServiceInfo> ServiceInfoRefresh;
        public event EventHandler<RegisterServiceInfo> ServiceDisconnect;
        bool SupportJmsDoc { get; }
        public IEnumerable<RegisterServiceInfo> GetAllRegisterServices();
        void AddRegisterService(IMicroServiceReception reception);
        void RemoveRegisterService(IMicroServiceReception reception);
        void RefreshServiceInfo(RegisterServiceInfo serviceInfo);
        RegisterServiceInfo GetServiceById(string id);

        void DisconnectAllServices();
        void DisconnectService(string id);
    }
}
