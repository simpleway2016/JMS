using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace WebApiTest.Controllers
{
    [Authorize]
    [ApiController]
    [MyActionFilter]
    [Route("[controller]")]
    public class WeatherForecast2Controller : ControllerBase
    {
        ApiTransactionDelegate _apiTransactionDelegate;
        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

        private readonly ILogger<WeatherForecast2Controller> _logger;


        public WeatherForecast2Controller(ApiTransactionDelegate apiTransactionDelegate, ILogger<WeatherForecast2Controller> logger)
        {
            this._apiTransactionDelegate = apiTransactionDelegate;
            _logger = logger;
        }


        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }

    class MyActionFilterAttribute : Attribute, IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
            var ctrl = context.HttpContext.RequestServices.GetService<ApiTransactionDelegate>();
            //ctrl.CommitAction = () =>
            //{
            //    Debug.WriteLine("提交事务了");
            //};
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {

        }
    }
}