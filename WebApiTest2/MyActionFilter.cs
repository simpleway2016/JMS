using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApiTest2
{
    public class MyActionFilter : IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
           
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            
        }
    }
}
