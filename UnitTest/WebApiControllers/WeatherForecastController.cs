using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Authorization;
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
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        ApiTransactionDelegate _apiTransactionDelegate;
        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };



        public WeatherForecastController(ApiTransactionDelegate apiTransactionDelegate)
        {
            this._apiTransactionDelegate = apiTransactionDelegate;
        }


        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get()
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

        [HttpPost]
        public string Post([FromForm]string name, [FromForm]int age)
        {
            return name + age;
        }

        [HttpPut]
        public FileContentResult Put()
        {
            byte[] bs = new byte[Request.Form.Files[0].Length];
            using (var ts = Request.Form.Files[0].OpenReadStream())
            {
                ts.Read(bs , 0 , bs.Length);
            }
            return new FileContentResult(bs, "application/stream");
        }
    }

    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string? Summary { get; set; }
    }
}