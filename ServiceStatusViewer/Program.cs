using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using JMS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStatusViewer.Infrastructures;
using ServiceStatusViewer.Views;
using Way.Lib;

namespace ServiceStatusViewer
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<AddressProvider>();
            Global.ServiceProvider = services.BuildServiceProvider();
            Task.Run(() => {
                //初始化数据库
                using (var db = new SysDBContext())
                {
                    db.InvokeHistory.FirstOrDefault();
                }
            });
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
