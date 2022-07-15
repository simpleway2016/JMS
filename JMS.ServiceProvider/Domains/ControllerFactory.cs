using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JMS.Domains
{
    internal class ControllerFactory
    {
        MicroServiceHost _microServiceHost;
        ConcurrentDictionary<string, ControllerTypeInfo> _controllerDict = new ConcurrentDictionary<string, ControllerTypeInfo>();
        public ControllerFactory(MicroServiceHost microServiceHost)
        {
            this._microServiceHost = microServiceHost;

        }

        public void SetControllerEnable(string serviceName,bool enable)
        {
            _controllerDict[serviceName].Enable = enable;
        }

        public void RegisterWebServer(string serviceName)
        {
            _controllerDict[serviceName] = new ControllerTypeInfo()
            {
                Enable = true
            };
        }

        /// <summary>
        /// 注册controller
        /// </summary>
        public void RegisterController(Type contollerType, string serviceName)
        {
            _controllerDict[serviceName] = new ControllerTypeInfo()
            {
                ServiceName = serviceName,
                Type = contollerType,
                Enable = true,
                NeedAuthorize = contollerType.GetCustomAttribute<AuthorizeAttribute>() != null,
                Methods = contollerType.GetTypeInfo().DeclaredMethods.Where(m =>
                    m.IsStatic == false &&
                    m.IsPublic &&
                    m.IsSpecialName == false &&
                    m.DeclaringType != typeof(MicroServiceControllerBase)
                    && m.DeclaringType != typeof(object)).OrderBy(m => m.Name).Select(m => new TypeMethodInfo
                    {
                        Method = m,
                        NeedAuthorize = m.GetCustomAttribute<AuthorizeAttribute>() != null
                    }).ToArray()
            };
        }

        public ControllerTypeInfo[] GetAllControllers()
        {
            return _controllerDict.Values.ToArray();
        }


        /// <summary>
        /// 获取controller类型信息
        /// </summary>
        /// <returns></returns>
        public ControllerTypeInfo GetControllerType(string serviceName) { 
            if(_controllerDict.TryGetValue(serviceName,out ControllerTypeInfo o))
            {
                return o;
            }
            return null;
        }




        /// <summary>
        /// 创建controller
        /// </summary>
        /// <returns></returns>
        public MicroServiceControllerBase CreateController(string serviceName)
        {
            if (_controllerDict.TryGetValue(serviceName, out ControllerTypeInfo o))
            {
                return (MicroServiceControllerBase)_microServiceHost.ServiceProvider.GetService(o.Type);
            }
            throw new Exception($"服务{serviceName}不存在");
        }

        public MicroServiceControllerBase CreateController(ControllerTypeInfo o)
        {
            return (MicroServiceControllerBase)_microServiceHost.ServiceProvider.GetService(o.Type);
        }
    }
}
