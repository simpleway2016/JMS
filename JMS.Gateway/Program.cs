using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using JMS.Interfaces;
using JMS.Impls;

namespace JMS
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();

            var port = configuration.GetValue<int>("Port");

            var datafolder = configuration.GetValue<string>("DataFolder");
            if (!System.IO.Directory.Exists(datafolder))
            {
                System.IO.Directory.CreateDirectory(datafolder);
            }

            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IRequestReception,RequestReception>();
            services.AddSingleton<ICommandHandlerManager, CommandHandlerManager>();
            services.AddSingleton<Gateway>();
            services.AddSingleton<LockKeyManager>();
            services.AddTransient<IMicroServiceReception,MicroServiceReception>();
            services.AddSingleton<TransactionIdBuilder>();

            var assembly = Assembly.Load(configuration.GetValue<string>("ServiceProviderAllocator:Assembly"));
            var serviceProviderAllocatorType = assembly.GetType(configuration.GetValue<string>("ServiceProviderAllocator:FullName"));
            var serviceProviderAllocator = (IServiceProviderAllocator)Activator.CreateInstance(serviceProviderAllocatorType);


            services.AddSingleton<IServiceProviderAllocator>(serviceProviderAllocator);
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<LockKeyManager>();
            
            var gateway = serviceProvider.GetService<Gateway>();
            gateway.ServiceProvider = serviceProvider;
            gateway.Run(port);
        }

    }
}
