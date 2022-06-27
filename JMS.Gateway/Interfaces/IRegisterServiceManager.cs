using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Interfaces
{
    /// <summary>
    /// 注册服务管理中心
    /// </summary>
    public interface IRegisterServiceManager
    {
        public event EventHandler<RegisterServiceInfo> ServiceConnect;
        public event EventHandler<RegisterServiceInfo> ServiceDisconnect;
        public IEnumerable<RegisterServiceInfo> GetAllRegisterServices();
        void AddRegisterService(IMicroServiceReception reception);
        void RemoveRegisterService(RegisterServiceInfo registerServiceInfo);
        RegisterServiceInfo GetServiceById(string id);

        void DisconnectAllServices();
        void DisconnectService(string id);
    }
}
