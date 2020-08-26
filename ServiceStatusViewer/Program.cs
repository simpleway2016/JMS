using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging.Serilog;
using Avalonia.ReactiveUI;
using JMS;
using Microsoft.Extensions.Configuration;
using ServiceStatusViewer.Views;
using Way.Lib;

namespace ServiceStatusViewer
{
    class Program
    {
        public static string SettingFileName;
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            SettingFileName = "appsettings.json";
            if (args != null && args.Length > 0)
                SettingFileName = args[0];

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile(SettingFileName, optional: true, reloadOnChange: true);
            Global.Configuration = builder.Build();

            //网关地址
            Global.GatewayAddresses = Global.Configuration.GetSection("Gateways").Get<NetAddress[]>();

            BuildAvaloniaApp()
              .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToDebug()
                .UseReactiveUI();
    }
}
