using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
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
                var filters = (from m in desc.FilterDescriptors
                               where m.Filter.GetType() != typeof(UnsupportedContentTypeFilter) &&
                               (m.Filter is TypeFilterAttribute || m.Filter.GetType().GetInterface(typeof(IActionFilter).FullName) != null)
                               select (m.Filter is TypeFilterAttribute) ? ((TypeFilterAttribute)m.Filter).ImplementationType : m.Filter.GetType()).ToArray();
                _actionfilters = new IActionFilter[filters.Length];
                for (int i = 0; i < filters.Length; i++)
                {
                    _actionfilters[i] = (IActionFilter)Activator.CreateInstance(filters[i]);
                }
                desc.Properties["jms_filters"] = _actionfilters;
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
