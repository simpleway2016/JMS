using JMS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebApiTest3
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", 8911) };
            services.RegisterJmsService("http://127.0.0.1:5000", "TestWebService", gateways);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

          

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseJmsService();


            Task.Run(() => {
                return;
                Thread.Sleep(2000);
                var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", 8911) };
                
                int total = 0;
                var startTime = DateTime.Now;
                var conCounter = app.ApplicationServices.GetService<IConnectionCounter>();
                while (true)
                {

                    using (var client = new RemoteClient(gateways))
                    {
                        //Thread.Sleep(10000000);
                        client.BeginTransaction();

                        var service = client.GetMicroService("TestWebService");
                        var ret = service.Invoke<WeatherForecast[]>("/WeatherForecast/Get");
                        //var service = client.GetMicroService("TestService");
                        //var ret = service.Invoke<WeatherForecast[]>("Get");
                        var ccCount = conCounter.ConnectionCount;
                        client.CommitTransaction();
                        total++;
                        var sec = (int)((DateTime.Now - startTime).TotalSeconds);
                        if (sec == 0)
                            sec = 1;
                        Debug.WriteLine($"通过一条,一共{total} 平均每秒：{total / sec} 连接数{ccCount}");
                    }
                }
            });
        }
    }
}
