using JMS.Common;
using JMS.HttpProxyDevice.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JMS.HttpProxyDevice
{
    public class HttpProxyDeviceProgram
    {
        internal static string AppSettingPath;
        internal static IConfiguration Configuration;
        internal static ConfigurationValue<AppConfig> Config;
        public static async Task Main(string[] args)
        {
            if (args.Length > 1 && args[0].EndsWith(".pfx"))
            {
                var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(args[0], args[1]);
                Console.WriteLine(cert.GetCertHashString());
                return;
            }
            //固定当前工作目录
            System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 10, Environment.ProcessorCount * 10);

            CommandArgParser cmdArg = new CommandArgParser(args);
            cmdArg.TryGetValue("-s",out AppSettingPath);

            if (AppSettingPath == null)
                AppSettingPath = "appsettings.json";

            var builder = new ConfigurationBuilder();
            if (AppSettingPath == "share")
            {
                AppSettingPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                AppSettingPath = Path.Combine(AppSettingPath, "jms.httpproxy");
                if (Directory.Exists(AppSettingPath) == false)
                {
                    Directory.CreateDirectory(AppSettingPath);
                }
                AppSettingPath = Path.Combine(AppSettingPath, "appsettings.json");
                if (File.Exists(AppSettingPath) == false)
                {
                    File.Copy("./appsettings.json", AppSettingPath);
                }
            }

            builder.AddJsonFile(AppSettingPath, optional: true, reloadOnChange: true);
            Configuration = builder.Build();
            Config = Configuration.GetNewest<AppConfig>();

            await Run(Configuration);
        }

        public static async Task Run(IConfiguration configuration)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });

            services.AddSingleton<ConnectionKeepAlive>();
            services.AddSingleton<ConnectionHandler>();

            var serviceProvider = services.BuildServiceProvider();

            var logger = serviceProvider.GetService<ILogger<HttpProxyDeviceProgram>>();
            logger.LogInformation($"版本号：{typeof(HttpProxyDeviceProgram).Assembly.GetName().Version}");
            logger?.LogInformation("配置文件:{0}", HttpProxyDeviceProgram.AppSettingPath);

            await serviceProvider.GetService<ConnectionKeepAlive>().RunAsync();
        }
    }
}
