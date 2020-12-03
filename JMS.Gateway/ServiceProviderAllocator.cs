using JMS.Dtos;
using Microsoft.Extensions.Logging;
using Natasha.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Way.Lib;

namespace JMS
{
    /// <summary>
    /// 微服务分配器，轮流分配原则
    /// </summary>
    class ServiceProviderAllocator : IServiceProviderAllocator
    {
        ILogger<ServiceProviderAllocator> _logger;
        ServiceProviderCounter[] _serviceInfos;

        public ServiceProviderAllocator(ILogger<ServiceProviderAllocator> logger)
        {
            this._logger = logger;

        }

        public void ServiceInfoChanged(RegisterServiceInfo[] serviceInfos)
        {
            if (_serviceInfos == null)
            {
                _serviceInfos = serviceInfos.Select(m => new ServiceProviderCounter(_logger)
                {
                    ServiceInfo = m
                }).ToArray();
            }
            else
            {
                List<ServiceProviderCounter> ret = new List<ServiceProviderCounter>();

                foreach( var info in serviceInfos )
                {
                    var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == info.Host && m.ServiceInfo.ServiceAddress == info.ServiceAddress && m.ServiceInfo.Port == info.Port);
                    if(item != null)
                    {
                        item.ServiceInfo = info;
                        ret.Add(item);
                    }
                    else
                    {
                        ret.Add(new ServiceProviderCounter(_logger)
                        {
                            ServiceInfo = info
                        });
                    }
                }

                _serviceInfos = ret.ToArray();
            }
        }
       
        public RegisterServiceLocation Alloc(GetServiceProviderRequest request)
        {
            var matchServices = _serviceInfos.Where(m => m.ServiceInfo.ServiceNames.Contains(request.ServiceName) && (m.ClientChecker == null || m.ClientChecker.Check(request.Arg)));

            //先查找cpu使用率低于70%的
            if(matchServices.Where(m => m.CpuUsage < 70).Count() > 0)
                matchServices = matchServices.Where(m => m.CpuUsage < 70);

            if (matchServices.Count() == 0)
                return null;
            //查找一个客户占用比较低的机器
            var item = matchServices.OrderBy(m => m.Usage).FirstOrDefault();
            Interlocked.Increment(ref item.RequestQuantity);
            item.Usage = item.RequestQuantity / (decimal)item.ServiceInfo.MaxThread;
            return new RegisterServiceLocation { 
                Host = item.ServiceInfo.Host,
                ServiceAddress = item.ServiceInfo.ServiceAddress,
                Port = item.ServiceInfo.Port
            };
        }

        public PerformanceInfo GetPerformanceInfo(RegisterServiceInfo from)
        {
            var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == from.Host && m.ServiceInfo.Port == from.Port);
            if (item != null)
            {
                return new PerformanceInfo { 
                    RequestQuantity = item.RequestQuantity,
                    CpuUsage = item.CpuUsage
                };
            }
            return null;
        }

        public void SetServicePerformanceInfo(RegisterServiceInfo from, PerformanceInfo performanceInfo)
        {
            var item = _serviceInfos.FirstOrDefault(m => m.ServiceInfo.Host == from.Host && m.ServiceInfo.Port == from.Port);
            if (item != null)
            {
                Interlocked.Exchange(ref item.RequestQuantity, performanceInfo.RequestQuantity.GetValueOrDefault());
                item.CpuUsage = performanceInfo.CpuUsage.GetValueOrDefault();
                item.Usage = item.RequestQuantity / (decimal)item.ServiceInfo.MaxThread;
            }
        }
    }

    class ServiceProviderCounter
    {
        ILogger _logger;

        public ServiceProviderCounter(ILogger logger)
        {
            this._logger = logger;

        }

        private RegisterServiceInfo _ServiceInfo;
        public RegisterServiceInfo ServiceInfo
        {
            get => _ServiceInfo;
            set
            {
                _ServiceInfo = value;

                if (!string.IsNullOrEmpty(value.ClientCheckCode))
                {
                    if (this.ClientChecker != null && this.ClientChecker.ClientCode == value.ClientCheckCode)
                    {

                    }
                    else
                    {
                        try
                        {
                            string text = @"
using System;
namespace HelloWorld
{
    public class Test : JMS.IClientCheck
    {
        public AssemblyCSharpBuilder CodeBuilder { get; set; }
        public string ClientCode { get; set; }
        public bool Check(string arg)
        {
            " + value.ClientCheckCode + @"
        }
    }
}";
                           
                            //根据脚本创建动态类
                            AssemblyCSharpBuilder oop = new AssemblyCSharpBuilder();
                            oop.ThrowCompilerError();
                            oop.ThrowSyntaxError();
                            oop.Compiler.Domain = DomainManagement.Random;
                            oop.Add(text);

                            Type type = oop.GetTypeFromShortName("Test");

                            _logger?.LogInformation("客户端检验代码编译通过，代码：{0}", value.ClientCheckCode);

                            if (this.ClientChecker != null)
                            {
                                try
                                {
                                    this.ClientChecker.CodeBuilder.Domain.Unload();
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, "ClientChecker Unload失败");
                                }
                            }


                            this.ClientChecker = (IClientCheck)Activator.CreateInstance(type);
                            this.ClientChecker.ClientCode = value.ClientCheckCode;
                            this.ClientChecker.CodeBuilder = oop;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "客户端检验代码编译错误，代码：{0}", value.ClientCheckCode);
                        }
                    }
                }
                else
                {
                    if(this.ClientChecker != null)
                    {
                        try
                        {
                            this.ClientChecker.CodeBuilder.Domain.Unload();
                        }
                        catch
                        {
                        }                       
                    }
                    this.ClientChecker = null;
                }
            }
        }

        public IClientCheck ClientChecker { get; private set; }

        /// <summary>
        /// 当前请求数量
        /// </summary>
        public int RequestQuantity;
        /// <summary>
        /// cpu使用率
        /// </summary>
        public double CpuUsage;
        public decimal Usage;
    }
}
