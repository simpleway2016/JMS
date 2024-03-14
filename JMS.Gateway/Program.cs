using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Linq;
using JMS.Common;
using System.Diagnostics;
using JMS.Applications;
using JMS.Infrastructures;
using System.Threading;
using System.IO;
using JMS.ServerCore;
using JMS.ServerCore.Http.Middlewares;
using JMS.ServerCore.Http;
using JMS.Applications.HttpMiddlewares;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JMS.ApiDocument;

namespace JMS
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length > 1&& args[0].EndsWith(".pfx") )
            {
                var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(args[0], args[1]);
                Console.WriteLine(cert.GetCertHashString());
                return;
            }
            //固定当前工作目录
            System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 10, Environment.ProcessorCount * 10);

            var gatewayBuilder = GatewayBuilder.Create(args);

           var gateway = gatewayBuilder.Build();

            gateway.Run();
        }

    }
}
