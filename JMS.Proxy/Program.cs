using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace JMS.Proxy
{
    class Program
    {
        static void Main(string[] args)
        {
            ThreadPool.GetMinThreads(out int w, out int c);
            if (c < 500)
            {
                ThreadPool.SetMinThreads(500, 500);
            }

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();

            var port = configuration.GetValue<int>("Port");

            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);

            services.AddSingleton<Proxy>();
            services.AddSingleton<RequestHandler>();
            var serviceProvider = services.BuildServiceProvider();

            var proxy = serviceProvider.GetService<Proxy>();
            //SSL
            var certPath = configuration.GetValue<string>("SSL:ServerCert");
            if (!string.IsNullOrEmpty(certPath))
            {
                proxy.ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, configuration.GetValue<string>("SSL:ServerPassword"));
                proxy.AcceptCertHash = configuration.GetSection("SSL:AcceptCertHash").Get<string[]>();
            }

            certPath = configuration.GetValue<string>("SSL:ClientCert");
            if (!string.IsNullOrEmpty(certPath))
            {
                proxy.ClientCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, configuration.GetValue<string>("SSL:ClientPassword"));
            }


            proxy.Run(port);
        }
    }
}
