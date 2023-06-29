using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using static JMS.ServiceProvider.AspNetCore.ApiFaildCommitBuilder;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class ControllerFactory
    {
        IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;
        public ControllerFactory(IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
        {
            this._actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;

        }
        public object Create(RequestInfo requestInfo,IServiceProvider serviceProvider, out ControllerActionDescriptor controllerActionDescriptor)
        {
            controllerActionDescriptor = null;
            foreach (var descitem in _actionDescriptorCollectionProvider.ActionDescriptors.Items)
            {
                if (descitem is ControllerActionDescriptor actionDesc)
                {
                    if (actionDesc.ActionName == requestInfo.Cmd.ActionName && actionDesc.ControllerTypeInfo.FullName == requestInfo.Cmd.ControllerFullName)
                    {
                        controllerActionDescriptor = actionDesc;
                        break;
                    }
                }
            }
            if (controllerActionDescriptor == null)
                return null;

            var type = controllerActionDescriptor.ControllerTypeInfo.AsType();
            var controller = serviceProvider.GetService(type);
            if (controller == null)
            {
                var constructor = type.GetConstructors().FirstOrDefault(m => m.IsPublic);
                var parameters = constructor.GetParameters();
                var pObjs = new object[parameters.Length];
                for (int i = 0; i < pObjs.Length; i++)
                {
                    pObjs[i] = serviceProvider.GetService(parameters[i].ParameterType);
                }
                controller = Activator.CreateInstance(type, pObjs);

            }
            return controller;
        }

        public object Create(string requestPath, IServiceProvider serviceProvider,out ControllerActionDescriptor controllerActionDescriptor)
        {
            controllerActionDescriptor = null;
           
            if (requestPath.StartsWith("/"))
                requestPath = requestPath.Substring(1);

            foreach (var descitem in _actionDescriptorCollectionProvider.ActionDescriptors.Items)
            {
                if (descitem is ControllerActionDescriptor actionDesc)
                {
                   
                    if (string.Equals(actionDesc.AttributeRouteInfo.Template, requestPath, StringComparison.OrdinalIgnoreCase) )
                    {
                        if (actionDesc.MethodInfo.ReturnType == typeof(void) && actionDesc.MethodInfo.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
                        {
                            throw new MethodDefineException($"请把{actionDesc.MethodInfo.DeclaringType.Name}.{actionDesc.MethodInfo.Name}()改为 async Task {actionDesc.MethodInfo.Name} 形式");
                        }
                        controllerActionDescriptor = actionDesc;
                        break;
                    }
                }
            }
            if (controllerActionDescriptor == null)
                return null;

            var type = controllerActionDescriptor.ControllerTypeInfo.AsType();
            var controller = serviceProvider.GetService(type);
            if (controller == null)
            {
                var constructor = type.GetConstructors().FirstOrDefault(m => m.IsPublic);
                var parameters = constructor.GetParameters();
                var pObjs = new object[parameters.Length];
                for (int i = 0; i < pObjs.Length; i++)
                {
                    pObjs[i] = serviceProvider.GetService(parameters[i].ParameterType);
                }
                controller = Activator.CreateInstance(type, pObjs);

            }
            return controller;
        }

        public object CreateByActionDescriptor(HttpContext context, IServiceProvider serviceProvider, ControllerActionDescriptor controllerActionDescriptor)
        {
            var type = controllerActionDescriptor.ControllerTypeInfo.AsType();
            var controller = serviceProvider.GetService(type);
            if (controller == null)
            {
                var constructor = type.GetConstructors().FirstOrDefault(m => m.IsPublic);
                var parameters = constructor.GetParameters();
                var pObjs = new object[parameters.Length];
                for (int i = 0; i < pObjs.Length; i++)
                {
                    pObjs[i] = serviceProvider.GetService(parameters[i].ParameterType);
                }
                controller = Activator.CreateInstance(type, pObjs);

            }
            return controller;
        }
    }
}
