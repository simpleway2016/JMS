using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTest.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class MyWeatherForecastController : ControllerBase
    {
        ApiTransactionDelegate _apiTransactionDelegate;
        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };



        public MyWeatherForecastController(ApiTransactionDelegate apiTransactionDelegate)
        {
            this._apiTransactionDelegate = apiTransactionDelegate;
        }

        [HttpGet]
        public async Task<IActionResult> SseExample()
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            for (int i = 0; i < 10; i++)
            {
                var content = $"data: {i}\n\n";

                await Response.WriteAsync(content);
                await Response.Body.FlushAsync();
            }

            return new EmptyResult();
        }

        [HttpGet]
        public async Task<IActionResult> ChunkedExample()
        {
            for (int i = 0; i < 10; i++)
            {
                var content = $"data: {i}\n\n";

                await Response.WriteAsync(content);
                await Response.Body.FlushAsync();
            }

            return new EmptyResult();
        }


        [HttpGet]
        public async ValueTask<IEnumerable<WeatherForecast>> Get()
        {
            await Task.Delay(1);
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

    }

   
}