using JMS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WebApiTest2
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        JMS.NetAddress[] gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", 8912) };
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddMvc(option => {
                option.Filters.Add<MyActionFilter>();
                option.Filters.Add<HttpGlobalExceptionFilter>();
            });
            services.RegisterJmsService("http://127.0.0.1:5000", "DemoService", gateways);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseJmsService();

            Task.Run(() =>
            {
                Thread.Sleep(2000);
                using (var rc = new RemoteClient(gateways))
                {
                    var service = rc.GetMicroService("DemoService");
                    var name = service.Invoke<string>("api/demo/Test", "Jack");
                    Console.WriteLine($"name:{name}");
                }
            });
            app.UseMvc();
        }
    }
}
