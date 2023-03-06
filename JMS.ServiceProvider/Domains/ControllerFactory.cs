using JMS.Dtos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Domains
{
    class ControllerFactory
    {
        MicroServiceHost _microServiceHost;
        ConcurrentDictionary<string, ControllerTypeInfo> _controllerDict = new ConcurrentDictionary<string, ControllerTypeInfo>();
        public ControllerFactory(MicroServiceHost microServiceHost)
        {
            this._microServiceHost = microServiceHost;

        }

        public void SetControllerEnable(string serviceName, bool enable)
        {
            _controllerDict[serviceName].Enable = enable;
        }

        public void RegisterWebServer(ServiceDetail serviceDetail)
        {
            _controllerDict[serviceDetail.Name] = new ControllerTypeInfo()
            {
                Enable = true,
                Service = serviceDetail
            };
        }

        /// <summary>
        /// 注册controller
        /// </summary>
        public void RegisterController(Type contollerType, ServiceDetail service)
        {
            var baseMethods = typeof(MicroServiceControllerBase).GetMethods().Select(m => m.Name).ToArray();
            var methods = contollerType.GetTypeInfo().DeclaredMethods.Where(m =>
                    m.IsStatic == false &&
                    m.IsPublic &&
                    m.IsSpecialName == false &&
                    m.DeclaringType != typeof(MicroServiceControllerBase) &&
                    baseMethods.Contains(m.Name) == false &&
                    m.DeclaringType != typeof(object)).OrderBy(m => m.Name).Select(m => new TypeMethodInfo
                    {
                        Method = m,
                        NeedAuthorize = m.GetCustomAttribute<AuthorizeAttribute>() != null,
                        AllowAnonymous = m.GetCustomAttribute<AllowAnonymousAttribute>() != null
                    }).ToArray();

            foreach (var method in methods)
            {
                if ( method.Method.ReturnType == typeof(void) && method.Method.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
                {
                    throw new MethodDefineException($"请把{method.Method.DeclaringType.Name}.{method.Method.Name}()改为 async Task {method.Method.Name} 形式");
                }
            }

            _controllerDict[service.Name] = new ControllerTypeInfo()
            {
                Service = service,
                Type = contollerType,
                Enable = true,
                NeedAuthorize = contollerType.GetCustomAttribute<AuthorizeAttribute>() != null,
                Methods = methods
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
        public ControllerTypeInfo GetControllerType(string serviceName)
        {
            if (_controllerDict.TryGetValue(serviceName, out ControllerTypeInfo o))
            {
                return o;
            }
            return null;
        }




        /// <summary>
        /// 创建controller
        /// </summary>
        /// <returns></returns>
        public MicroServiceControllerBase CreateController(IServiceScope serviceScope, string serviceName)
        {
            if (_controllerDict.TryGetValue(serviceName, out ControllerTypeInfo o))
            {
                return (MicroServiceControllerBase)serviceScope.ServiceProvider.GetService(o.Type);
            }
            throw new Exception($"服务{serviceName}不存在");
        }

        public object CreateController(IServiceScope serviceScope, ControllerTypeInfo o)
        {
            return serviceScope.ServiceProvider.GetService(o.Type);
        }
    }
}
