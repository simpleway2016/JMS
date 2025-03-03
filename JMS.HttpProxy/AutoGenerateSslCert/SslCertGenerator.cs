using Jack.Acme;
using JMS.HttpProxy.Dtos;
using JMS.HttpProxy.Servers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.AutoGenerateSslCert
{
    internal class SslCertGenerator
    {
        private readonly ILogger<SslCertGenerator> _logger;
        ConcurrentDictionary<HttpServer, bool> _httpServers = new ConcurrentDictionary<HttpServer, bool>();
        ConcurrentDictionary<string, DomainSslCertGenerator> _Generators = new ConcurrentDictionary<string, DomainSslCertGenerator>();
        public SslCertGenerator(ILogger<SslCertGenerator> logger)
        {
            this._logger = logger; 
           
        }

        public void OnCertBuilded(string domain,X509Certificate2 cert, string path,string password)
        {
            try
            {
                var matchServers = _httpServers.Where(m => m.Key.Config.SSL.Acme.Domain == domain).Select(m => m.Key).ToArray();
                foreach (var matchServer in matchServers)
                {
                    if (matchServer.Certificate == null || cert.NotAfter.ToUniversalTime() > matchServer.Certificate.NotAfter.ToUniversalTime())
                    {
                        matchServer.Certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password);
                    }
                }

                //防止多个server同时用同一个证书对象
                cert.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
            }
        }

        public void AddRequest(HttpServer httpServer)
        {
            _httpServers[httpServer] = true;

            var generator = _Generators.GetOrAdd(httpServer.Config.SSL.Acme.Domain, domain => new DomainSslCertGenerator(this , httpServer.Config.SSL.Acme,_logger));
            generator.Run();
        }
        public void RemoveRequest(HttpServer httpServer,string domain)
        {
            if (_httpServers.TryRemove(httpServer, out _))
            {
                try
                {
                    if (_httpServers.Any(m => m.Key.Config.SSL?.Acme?.Domain == domain) == false)
                    {
                        if (_Generators.TryRemove(domain, out DomainSslCertGenerator domainSslCertGenerator))
                        {
                            domainSslCertGenerator.Dispose();
                            try
                            {
                                _logger.LogInformation($"移除Acme域名：{domain}");
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "");
                }
            }
        }

      
    }

    class DomainSslCertGenerator:IDisposable
    {
        private readonly SslCertGenerator _sslCertGenerator;
        private readonly AcmeConfig _acmeConfig;
        ILogger _logger;
        bool _isStop;
        public DomainSslCertGenerator(SslCertGenerator sslCertGenerator,AcmeConfig acmeConfig, ILogger logger)
        {
            this._sslCertGenerator = sslCertGenerator;
            this._acmeConfig = acmeConfig;
            _logger = logger;
        }

        public void Dispose()
        {
            _isStop = true;
        }

        public void Run()
        {
            generate();
        }

     

        async void generate()
        {
            var pwd = "123456";
            var path = $"${_acmeConfig.Domain}.pfx";
            var path_expire = $"${_acmeConfig.Domain}.pfx2";

            var services = new ServiceCollection();
            services.AddCertificateGenerator();
            if (_acmeConfig.DomainProvider == DomainProvider.AlibabaCloud)
            {
                services.AddAlibabaCloudRecordWriter(_acmeConfig.AccessKeyId, _acmeConfig.AccessKeySecret);
            }
            else
            {
                _logger.LogInformation($"DomainProvider无效：{_acmeConfig.DomainProvider}");
                return;
            }
            using var serviceProvider = services.BuildServiceProvider();

            _logger.LogInformation($"开始载入或自动生成ssl证书，域名：{_acmeConfig.Domain}");
            X509Certificate2 cert;
            var certificateGenerator = serviceProvider.GetRequiredService<ICertificateGenerator>();
            bool isFirstLoad = true;
            while (!_isStop)
            {
                try
                {
                    if (File.Exists(path) && File.Exists(path_expire))
                    {                       
                        if (DateTime.TryParse(File.ReadAllText(path_expire, Encoding.UTF8), out DateTime expireTime))
                        {
                            if (expireTime.ToUniversalTime() > DateTime.UtcNow.AddDays(_acmeConfig.PreDays))
                            {
                                if (isFirstLoad)
                                {
                                    try
                                    {
                                        cert = X509CertificateLoader.LoadPkcs12FromFile(path, pwd);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "");
                                        File.Delete(path);
                                        continue;
                                    }
                                    //有效期大于5天
                                    _logger.LogInformation($"域名：{_acmeConfig.Domain} 使用已有证书{path}，有效期到：{cert.NotAfter.ToLongDateString()}");
                                    _sslCertGenerator.OnCertBuilded(_acmeConfig.Domain, cert,path,pwd);
                                    isFirstLoad = false;
                                }
                                await Task.Delay(60000);
                                continue;
                            }
                        }
                    }

                    await certificateGenerator.GeneratePfxAsync(_acmeConfig.Domain, new CsrInformation
                    {
                        CountryName = "CA",
                        State = "Ontario",
                        Locality = "Toronto",
                        Organization = "Certes",
                        OrganizationUnit = "Dev",
                    }, path, pwd);
                   
                    cert = X509CertificateLoader.LoadPkcs12FromFile(path, pwd);
                    File.WriteAllText(path_expire, cert.NotAfter.ToUniversalTime().ToString("R"), Encoding.UTF8);

                    _logger.LogInformation($"域名：{_acmeConfig.Domain} 成功生成证书{path}，有效期到：{cert.NotAfter.ToLongDateString()}");
                    _sslCertGenerator.OnCertBuilded(_acmeConfig.Domain, cert, path, pwd);
                    isFirstLoad = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "");
                }
                finally
                {
                    await Task.Delay(60000);
                }
            }
        }
    }
}
