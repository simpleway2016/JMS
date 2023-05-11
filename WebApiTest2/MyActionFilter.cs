using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace WebApiTest2
{
    public class MyActionFilter : IActionFilter
    {
        ILogger<MyActionFilter> _logger;
        public MyActionFilter(ILogger<MyActionFilter> logger)
        {
            this._logger = logger;

        }
        public void OnActionExecuted(ActionExecutedContext context)
        {
           if(context.Exception != null)
            {
                _logger.LogError(context.Exception, "");
            }
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            
        }
    }
}
