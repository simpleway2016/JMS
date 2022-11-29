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
        public HttpInvokeConnect(string serviceName, RegisterServiceLocation location, Invoker invoker)
        {
            InvokingInfo = new InvokingInformation();
            InvokingInfo.ServiceName = serviceName;
            InvokingInfo.ServiceLocation = location;
            Invoker = invoker;
        }

        byte[] createHttpDatas(IRemoteClient tran, Uri uri,string protocol , string method)
        {
            StringBuilder headerStr = new StringBuilder();
            if (tran != null)
            {
                var headers = tran.GetCommandHeader();
                foreach (var pair in headers)
                {
                    headerStr.AppendLine($"{pair.Key}: {pair.Value}");
                }
            }
            if(headerStr.Length == 0)
            {
                headerStr.AppendLine("");
            }
            var content = @"GET " + method + @" HTTP/1.1
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

        public T Invoke<T>(string method, IRemoteClient tran, params object[] parameters)
        {
            return Invoke<T>(method, tran, tran.GetCommandHeader(), parameters);
        }

        public T Invoke<T>(string method, IRemoteClient tran, Dictionary<string, string> headers, params object[] parameters)
        {
            if (tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            InvokingInfo.MethodName = method;
            InvokingInfo.Parameters = parameters;

            var uri = new Uri(InvokingInfo.ServiceLocation.ServiceAddress);
            _client = NetClientPool.CreateClient(null, uri.Host, uri.Port, null, (client) => {
                if (uri.Scheme == "https")
                {
                    client.AsSSLClient(uri.Host, new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) => true));
                }
            });
            try
            {

                _client.ReadTimeout = tran.Timeout;
                var data = createHttpDatas(tran, uri, "JmsService", method);
                _client.Write(data);

                _client.WriteServiceData(Encoding.UTF8.GetBytes(parameters.GetStringArrayParameters().ToJsonString()));
                var result = _client.ReadServiceObject<InvokeResult<T>>();
                if (result.Success == false)
                {
                    this.AddClientToPool();
                    throw new RemoteException(tran.TransactionId, result.Error);
                }

                if (result.SupportTransaction)
                    tran.AddConnect(this);
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
        public async Task<T> InvokeAsync<T>(string method, IRemoteClient tran, params object[] parameters)
        {
            var headers = tran.GetCommandHeader();
            if (tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            InvokingInfo.MethodName = method;
            InvokingInfo.Parameters = parameters;

            var uri = new Uri(InvokingInfo.ServiceLocation.ServiceAddress);
            _client = await NetClientPool.CreateClientAsync(null, uri.Host, uri.Port, null, (client) => {
                if (uri.Scheme == "https")
                {
                    return client.AsSSLClientAsync(uri.Host, new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) => true));
                }
                return Task.CompletedTask;
            });
            try
            {

                _client.ReadTimeout = tran.Timeout;
                var data = createHttpDatas(tran, uri, "JmsService", method);
                _client.Write(data);

                _client.WriteServiceData(Encoding.UTF8.GetBytes(parameters.GetStringArrayParameters().ToJsonString()));
                var result = await _client.ReadServiceObjectAsync<InvokeResult<T>>();
                if (result.Success == false)
                {
                    this.AddClientToPool();
                    throw new RemoteException(tran.TransactionId, result.Error);
                }

                if (result.SupportTransaction)
                    tran.AddConnect(this);
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

        public InvokeResult GoReadyCommit(IRemoteClient tran)
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

        public async Task<InvokeResult> GoReadyCommitAsync(IRemoteClient tran)
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

        public InvokeResult GoCommit(IRemoteClient tran)
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

        public async Task<InvokeResult> GoCommitAsync(IRemoteClient tran)
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

        public InvokeResult GoRollback(IRemoteClient tran)
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

        public async Task<InvokeResult> GoRollbackAsync(IRemoteClient tran)
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
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public void RetryTranaction(NetAddress proxyAddress, RegisterServiceLocation serviceLocation, byte[] cert, string tranId,string tranFlag)
        {
            var uri = new Uri(serviceLocation.ServiceAddress);
            _client = NetClientPool.CreateClient(null, uri.Host, uri.Port, null, (client) => {
                if (uri.Scheme == "https")
                {
                    client.AsSSLClient(uri.Host, new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) => true));
                }
            });

            var data = createHttpDatas(null, uri, "JmsRetry", "/" + tranId + "," + tranFlag);
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
