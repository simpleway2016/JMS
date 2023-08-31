using JMS.Dtos;
using JMS.TransactionReporters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    class HttpInvokeConnect : IInvokeConnect
    {

        NetClient _client;

        public InvokingInformation InvokingInfo { get; private set; }
        public InvokeCommand Command { get; private set; }
        public Invoker Invoker { get; private set; }
        public bool HasTransactionHolding { get;private set; }

        public HttpInvokeConnect(string serviceName, ClientServiceDetail location, Invoker invoker)
        {
            InvokingInfo = new InvokingInformation();
            InvokingInfo.ServiceName = serviceName;
            InvokingInfo.ServiceLocation = location;
            Invoker = invoker;
        }

        byte[] createHttpDatas(RemoteClient tran, Uri uri,string protocol)
        {
            StringBuilder headerStr = new StringBuilder();
            if (tran != null)
            {
                var headers = tran.GetCommandHeader();
                foreach (var pair in headers)
                {
                    headerStr.Append($"{pair.Key}: {pair.Value}\r\n");
                }
            }
            if (headerStr.Length == 0)
            {
                headerStr.Append("\r\n");
            }

            var content = @"GET " + uri.AbsolutePath + @" HTTP/1.1
Host: " + uri.Authority + @"
Connection: keep-alive
Protocol: "+protocol+@"
User-Agent: JmsInvoker
Accept: text/html
Accept-Encoding: gzip, deflate, br
Accept-Language: zh-CN,zh;q=0.9
"+ headerStr +@"
";
            return Encoding.UTF8.GetBytes(content);
        }

        public T Invoke<T>(string method, RemoteClient tran, params object[] parameters)
        {
            return Invoke<T>(method, tran, tran.GetCommandHeader(), parameters);
        }

        Uri getServiceUri(string serviceAddress , string method)
        {
            if(serviceAddress.EndsWith('/') == false && method.StartsWith('/') == false)
            {
                return new Uri($"{serviceAddress}/{method}");
            }
            else
            {
                return new Uri($"{serviceAddress}{method}");
            }
        }

        public T Invoke<T>(string method, RemoteClient tran, Dictionary<string, string> headers, params object[] parameters)
        {
            if (tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            InvokingInfo.MethodName = method;
            InvokingInfo.Parameters = parameters;

            var uri = getServiceUri(InvokingInfo.ServiceLocation.ServiceAddress , method);
            bool isNewClient = false;
            if (_client == null)
            {
                _client = NetClientPool.CreateClient(tran.ProxyAddress, new NetAddress(uri.Host, uri.Port) { Certificate = tran.ServiceClientCertificate }, (client) =>
                {
                    if (uri.Scheme == "https")
                    {
                        client.AsSSLClient(uri.Host, new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) => true));
                    }
                });
                isNewClient = true;
            }
            try
            {

                _client.ReadTimeout = tran.Timeout;
                if (isNewClient)
                {
                    var data = createHttpDatas(tran, uri, "JmsService");
                    _client.Write(data);
                }
                else
                {
                    _client.WriteServiceData(Encoding.UTF8.GetBytes("invoke"));
                    _client.WriteServiceData(Encoding.UTF8.GetBytes(uri.PathAndQuery));
                }
                _client.WriteServiceData(Encoding.UTF8.GetBytes(parameters.GetStringArrayParameters().ToJsonString()));
                var result = _client.ReadServiceObject<InvokeResult<T>>();
                if (result.Success == false)
                {
                    this.AddClientToPool();
                    throw new RemoteException(tran.TransactionId, result.Error);
                }

                if (result.SupportTransaction)
                {
                    this.HasTransactionHolding = true;
                }
                else
                {
                    this.AddClientToPool();
                    this.Dispose();
                }

                return result.Data;
            }
            catch (ConvertException ex)
            {
                InvokeResult<string> otherObj = null;
                try
                {
                    otherObj = ex.Source.FromJson<InvokeResult<string>>();
                }
                catch
                {

                }

                if (otherObj.Success == false)
                {
                    throw new RemoteException(tran.TransactionId, otherObj.Error);
                }

                if (otherObj != null)
                    throw new ConvertException(otherObj.Data, $"无法将{ex.Source}里面的Data实例化为{typeof(T).FullName}");

                throw ex;
            }
            catch (Exception)
            {
                this.Dispose();
                throw;
            }


        }
        public async Task<T> InvokeAsync<T>(string method, RemoteClient tran, params object[] parameters)
        {
            var headers = tran.GetCommandHeader();
            if (tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            InvokingInfo.MethodName = method;
            InvokingInfo.Parameters = parameters;
            var isNewClient = false;
            var uri = getServiceUri(InvokingInfo.ServiceLocation.ServiceAddress , method);

            if (_client == null)
            {
                _client = await NetClientPool.CreateClientAsync(tran.ProxyAddress, new NetAddress(uri.Host, uri.Port) { Certificate = tran.ServiceClientCertificate }, (client) =>
                {
                    if (uri.Scheme == "https")
                    {
                        return client.AsSSLClientAsync(uri.Host, new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) => true));
                    }
                    return Task.CompletedTask;
                });
                isNewClient = true;
            }
            try
            {

                _client.ReadTimeout = tran.Timeout;
                if (isNewClient)
                {
                    var data = createHttpDatas(tran, uri, "JmsService");
                    _client.Write(data);
                }
                else
                {
                    _client.WriteServiceData(Encoding.UTF8.GetBytes("invoke"));
                    _client.WriteServiceData(Encoding.UTF8.GetBytes(uri.PathAndQuery));
                }

                _client.WriteServiceData(Encoding.UTF8.GetBytes(parameters.GetStringArrayParameters().ToJsonString()));
                var result = await _client.ReadServiceObjectAsync<InvokeResult<T>>();
                if (result.Success == false)
                {
                    this.AddClientToPool();
                    throw new RemoteException(tran.TransactionId, result.Error);
                }

                if (result.SupportTransaction)
                {
                    this.HasTransactionHolding = true;
                }
                else
                {
                    this.AddClientToPool();
                    this.Dispose();
                }

                return result.Data;
            }
            catch (ConvertException ex)
            {
                InvokeResult<string> otherObj = null;
                try
                {
                    otherObj = ex.Source.FromJson<InvokeResult<string>>();
                }
                catch
                {

                }

                if (otherObj.Success == false)
                {
                    throw new RemoteException(tran.TransactionId, otherObj.Error);
                }

                if (otherObj != null)
                    throw new ConvertException(otherObj.Data, $"无法将{ex.Source}里面的Data实例化为{typeof(T).FullName}");

                throw ex;
            }
            catch (Exception)
            {
                this.Dispose();
                throw;
            }

        }

        public InvokeResult GoReadyCommit(RemoteClient tran)
        {
            _client.WriteServiceData(Encoding.UTF8.GetBytes("ready"));
            var ret = _client.ReadServiceData();
            if (ret == "ok")
            {
                return new InvokeResult
                {
                    Success = true
                };
            }
            else
            {
                return new InvokeResult
                {
                    Success = false
                };
            }
        }

        public async Task<InvokeResult> GoReadyCommitAsync(RemoteClient tran)
        {
            _client.WriteServiceData(Encoding.UTF8.GetBytes("ready"));
            var ret = await _client.ReadServiceDataAsync();
            if (ret == "ok")
            {
                return new InvokeResult
                {
                    Success = true
                };
            }
            else
            {
                return new InvokeResult
                {
                    Success = false
                };
            }
        }

        public InvokeResult GoCommit(RemoteClient tran)
        {
            _client.WriteServiceData(Encoding.UTF8.GetBytes("commit"));
            var ret = _client.ReadServiceData();
            if (ret == "ok")
            {
                return new InvokeResult
                {
                    Success = true
                };
            }
            else
            {
                return new InvokeResult
                {
                    Success = false
                };
            }
        }

        public async Task<InvokeResult> GoCommitAsync(RemoteClient tran)
        {
            _client.WriteServiceData(Encoding.UTF8.GetBytes("commit"));
            var ret = await _client.ReadServiceDataAsync();
            if (ret == "ok")
            {
                return new InvokeResult
                {
                    Success = true
                };
            }
            else
            {
                return new InvokeResult
                {
                    Success = false
                };
            }
        }

        public InvokeResult GoRollback(RemoteClient tran)
        {
            _client.WriteServiceData(Encoding.UTF8.GetBytes("rollback"));
            var ret = _client.ReadServiceData();
            if (ret == "ok")
            {
                return new InvokeResult
                {
                    Success = true
                };
            }
            else
            {
                return new InvokeResult
                {
                    Success = false
                };
            }
        }

        public async Task<InvokeResult> GoRollbackAsync(RemoteClient tran)
        {
            _client.WriteServiceData(Encoding.UTF8.GetBytes("rollback"));
            var ret = await _client.ReadServiceDataAsync();
            if (ret == "ok")
            {
                return new InvokeResult
                {
                    Success = true
                };
            }
            else
            {
                return new InvokeResult
                {
                    Success = false
                };
            }
        }

        public void AddClientToPool()
        {
            byte[] data = new byte[1024];
            int offset = 0;
            while (true)
            {
                var len = _client.InnerStream.Read(data , offset, data.Length - offset);
                offset += len;
                if (data[offset - 1] == 10 && data[offset - 2] == 13 && data[offset - 3] == 10 && data[offset - 4] == 13)
                    break;
            }
            NetClientPool.AddClientToPool(_client);
            _client = null;
        }

        public void Dispose()
        {
            this.HasTransactionHolding = false;
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public void RetryTranaction(NetAddress proxyAddress, ClientServiceDetail serviceLocation, byte[] certData, string tranId,string tranFlag)
        {
            X509Certificate2 cert = null;
            if (certData != null)
            {
                cert = new X509Certificate2(certData);
            }
            var uri = getServiceUri(serviceLocation.ServiceAddress, $"/{tranId},{tranFlag}");
            _client = NetClientPool.CreateClient(proxyAddress,new NetAddress(uri.Host, uri.Port) { Certificate = cert }, (client) => {
                if (uri.Scheme == "https")
                {
                    client.AsSSLClient(uri.Host, new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) => true));
                }
            });

            var data = createHttpDatas(null, uri, "JmsRetry");
            _client.Write(data);

            var result = _client.ReadServiceObject<InvokeResult<int>>();
            if (result.Success && result.Data == 0)
            {
                TransactionReporterRoute.Logger?.LogInformation($"{serviceLocation.ServiceAddress}:{serviceLocation.Port}重新执行事务{tranId}成功");
            }

            this.AddClientToPool();
            if (result.Success == false || result.Data == -2)
            {
                if (string.IsNullOrEmpty(result.Error))
                    throw new Exception("重新执行事务失败");
                else
                    throw new Exception(result.Error);
            }
        }
    }
}
