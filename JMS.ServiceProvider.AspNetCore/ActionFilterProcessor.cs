using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMS.ServiceProvider.AspNetCore
{
    internal class ActionFilterProcessor
    {
        ActionExecutedContext _actionContext = null;
        ActionExecutingContext _excutingContext = null;
        IActionFilter[] _actionfilters = null;
        object _controller;
        IActionFilter _mvcController;
        internal Exception Exception;
        public ActionFilterProcessor(HttpContext context,object controller,ControllerActionDescriptor desc, object[] parameters)
        {
            _controller = controller;
            _mvcController = controller as IActionFilter;
            
            var hasFilter = false;

             
            if (desc.Properties.TryGetValue("jms_filters", out object o) == false)
            {
                ILogger<ActionFilterProcessor> logger = context.RequestServices.GetService<ILogger<ActionFilterProcessor>>();
                var filters = (from m in desc.FilterDescriptors
                               where !(m.Filter is UnsupportedContentTypeFilter) &&
                               !(m.Filter is Microsoft.AspNetCore.Mvc.Infrastructure.ModelStateInvalidFilter) &&
                               (m.Filter is TypeFilterAttribute || m.Filter.GetType().GetInterface(typeof(IActionFilter).FullName) != null)
                               select (m.Filter is TypeFilterAttribute) ? ((TypeFilterAttribute)m.Filter).ImplementationType : m.Filter.GetType()).ToArray();
                filters = filters.Where(m => m.GetInterface(typeof(IActionFilter).FullName) != null).ToArray();

                _actionfilters = new IActionFilter[filters.Length];
                for (int i = 0; i < filters.Length; i++)
                {
                    try
                    {
                        var contructor = filters[i].GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).FirstOrDefault();
                        var contructorParams = contructor.GetParameters();
                        var ps = new object[contructorParams.Length];
                        for (int j = 0; j < ps.Length; j++)
                        {
                            ps[j] = context.RequestServices.GetService(contructorParams[j].ParameterType);
                        }
                        _actionfilters[i] = (IActionFilter)Activator.CreateInstance(filters[i], ps);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, $"实例化 {filters[i].FullName} 发生异常");
                    }
                }
                desc.Properties["jms_filters"] = _actionfilters.Where(m=>m != null).ToArray();
            }
            else
            {
                _actionfilters = (IActionFilter[])o;
            }

            if (_mvcController != null || _actionfilters.Length > 0)
            {
                hasFilter = true;
               
            }

            if (hasFilter)
            {
                var ac = new ActionContext(context, new RouteData(), desc);
                _actionContext = new ActionExecutedContext(ac, new List<IFilterMetadata>(), controller);
                Dictionary<string, object> dict = new Dictionary<string, object>();
                for (int i = 0; i < desc.Parameters.Count; i++)
                {
                    dict[desc.Parameters[i].Name] = parameters[i];
                }
                _excutingContext = new ActionExecutingContext(ac, new List<IFilterMetadata>(), dict, controller);
            }
        }

        public object OnActionExecuted(object result)
        {
            if (_actionContext == null)
                return result;

            if (result != null && !(result is IActionResult))
            {
                _actionContext.Result = new ObjectResult(result);
            }
            else
            {
                _actionContext.Result = result as IActionResult;
            }
            _actionContext.Exception = this.Exception;

            if (_mvcController != null)
            {
                _mvcController.OnActionExecuted(_actionContext);
            }

            for(int i = _actionfilters.Length - 1; i >= 0; i--)
            {
                _actionfilters[i].OnActionExecuted(_actionContext);
            }

            
            return _actionContext.Result;
        }

        public void OnActionExecuting()
        {
            if(_excutingContext != null)
            {
                foreach(var filter in _actionfilters)
                {
                    filter.OnActionExecuting(_excutingContext);
                }
            }
            if(_mvcController != null)
            {
                _mvcController.OnActionExecuting(_excutingContext);
            }
        }
    }
}
