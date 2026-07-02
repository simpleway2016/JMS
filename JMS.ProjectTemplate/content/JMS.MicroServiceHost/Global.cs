using JMS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.MicroServiceHost
{
    internal class Global
    {
        public static IServiceProvider ServiceProvider
        {
            get;
            set;
        }

        public static IConfiguration Configuration
        {
            get;
            set;
        }

        public static NetAddress[] GatewayAddresses
        {
            get;
            set;
        }

        public static void InitSerilogLog(ServiceCollection services)
        {

            var log = new LoggerConfiguration()
             .ReadFrom.Configuration(Configuration)
              // 最小的日志输出级别
              //.MinimumLevel.Error()
              // 日志调用类命名空间如果以 Microsoft 开头，覆盖日志输出最小级别为 Information
              //.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
              .Enrich.FromLogContext()
             // 配置日志输出到文件，文件输出到当前项目的 logs 目录下
             .WriteTo.Logger(lc =>
             {
                 lc.Filter.ByExcluding(e => e.Level == LogEventLevel.Error || e.Level == LogEventLevel.Debug)
                 .WriteTo.File("logs/normal/log.txt",
                  outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] {SourceContext} {NewLine}{Message}{NewLine}{Exception}",
                     rollingInterval: RollingInterval.Day,
                     fileSizeLimitBytes: 1024 * 1024,
                     rollOnFileSizeLimit: true);
             })
              .WriteTo.Logger(lc =>
              {
                  lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Error)
                  .WriteTo.File("logs/errors/log.txt",
                   outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] {SourceContext} {NewLine}{Message}{NewLine}{Exception}",
                      rollingInterval: RollingInterval.Day,
                      fileSizeLimitBytes: 1024 * 1024,
                      rollOnFileSizeLimit: true);
              })
               .WriteTo.Logger(lc =>
               {
                   lc.Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Debug)
                   .WriteTo.File("logs/debugs/log.txt",
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] {SourceContext} {NewLine}{Message}{NewLine}{Exception}",
                       rollingInterval: RollingInterval.Day,
                       fileSizeLimitBytes: 1024 * 1024,
                       rollOnFileSizeLimit: true);
               });

            log.WriteTo.Console();

            // 创建 logger
            Log.Logger = log.CreateLogger();

            services.AddLogging(builder =>
            {
                builder.AddSerilog();
            });
        }
    }
}
