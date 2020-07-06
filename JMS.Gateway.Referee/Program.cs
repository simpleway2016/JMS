using JMS.Impls;
using JMS.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace JMS.Gateway
{
    class Program
    {
        static void Main(string[] args)
        {
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
            services.AddTransient<MasterGatewayConnector>();
            services.AddSingleton<IRequestReception,RequestReception>();
            services.AddSingleton<ICommandHandlerManager, CommandHandlerManager>();
            services.AddSingleton<Referee>();

            var serviceProvider = services.BuildServiceProvider();

            var referee = serviceProvider.GetService<Referee>();
            referee.Run(port);
        }
    }
}
