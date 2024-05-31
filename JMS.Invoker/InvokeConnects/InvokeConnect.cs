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
    public class InvokingInformation
    {
        public ClientServiceDetail ServiceLocation { get; internal set; }
        public string ServiceName
        {
            get;
            internal set;
        }
        public string MethodName { get; internal set; }
        public object[] Parameters { get; internal set; }
    }
    class InvokeConnect : IInvokeConnect
    {

        NetClient _client;

        public InvokingInformation InvokingInfo { get; private set; }
        public InvokeCommand Command { get; private set; }
        public Invoker Invoker { get; private set; }

        public bool HasTransactionHolding { get; private set; }
        public InvokeConnect(string serviceName, ClientServiceDetail location, Invoker invoker)
        {
            InvokingInfo = new InvokingInformation();
            InvokingInfo.ServiceName = serviceName;
            InvokingInfo.ServiceLocation = location;
            Invoker = invoker;
        }


        public T Invoke<T>(string method, RemoteClient tran, params object[] parameters)
        {
            return Invoke<T>(method, tran, tran.GetCommandHeader(), parameters);
        }

        public T Invoke<T>(string method, RemoteClient tran, Dictionary<string, string> headers, params object[] parameters)
        {
            if (tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            InvokingInfo.MethodName = method;
            InvokingInfo.Parameters = parameters;

            if (_client == null)
            {
                _client = NetClientPool.CreateClient(tran.ProxyAddress, new NetAddress(InvokingInfo.ServiceLocation.ServiceAddress, InvokingInfo.ServiceLocation.Port, InvokingInfo.ServiceLocation.UseSsl) { Certificate = tran.ServiceClientCertificate, CertDomain = InvokingInfo.ServiceLocation.ServiceAddress });
            }

            try
            {
                _client.ReadTimeout = tran.Timeout;
                Command = new InvokeCommand()
                {
                    Header = headers,
                    Service = InvokingInfo.ServiceName,
                    Method = method,
                    Parameters = parameters.Length == 0 ? null :
                                    parameters.GetStringArrayParameters()
                };


                _client.WriteServiceData(Command);

                var originalSize = _client.MaxCommandSize;
                _client.MaxCommandSize = int.MaxValue;
                var result = _client.ReadServiceObject<InvokeResult<T>>();
                _client.MaxCommandSize = originalSize;

                if (result.Success == false)
                {
                    this.AddClientToPool();
                    throw new RemoteException(tran.TransactionId, result.GetStatusCode(), result.Error);
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
                    throw new RemoteException(tran.TransactionId, otherObj.GetStatusCode(), otherObj.Error);
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
            var ret = await InvokeExAsync<T>(method, tran, parameters);
            return ret.Data;
        }

        public async Task<InvokeResult<T>> InvokeExAsync<T>(string method, RemoteClient tran, params object[] parameters)
        {
            var headers = tran.GetCommandHeader();

            if (tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            InvokingInfo.MethodName = method;
            InvokingInfo.Parameters = parameters;

            if (_client == null)
            {
                _client = await NetClientPool.CreateClientAsync(tran.ProxyAddress, new NetAddress(InvokingInfo.ServiceLocation.ServiceAddress, InvokingInfo.ServiceLocation.Port, InvokingInfo.ServiceLocation.UseSsl) { Certificate = tran.ServiceClientCertificate, CertDomain = InvokingInfo.ServiceLocation.ServiceAddress });
            }

            try
            {
                _client.ReadTimeout = tran.Timeout;
                Command = new InvokeCommand()
                {
                    Header = headers,
                    Service = InvokingInfo.ServiceName,
                    Method = method,
                    Parameters = parameters.Length == 0 ? null :
                                    parameters.GetStringArrayParameters()
                };


                _client.WriteServiceData(Command);

                var originalSize = _client.MaxCommandSize;
                _client.MaxCommandSize = int.MaxValue;
                var result = await _client.ReadServiceObjectAsync<InvokeResult<T>>();
                _client.MaxCommandSize = originalSize;

                if (result.Success == false)
                {
                    this.AddClientToPool();
                    throw new RemoteException(tran.TransactionId, result.GetStatusCode(), result.Error);
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

                return result;
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
                    throw new RemoteException(tran.TransactionId, otherObj.GetStatusCode(), otherObj.Error);
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
            _client.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.HealthyCheck,
            });
            return _client.ReadServiceObject<InvokeResult>();
        }
        public Task<InvokeResult> GoReadyCommitAsync(RemoteClient tran)
        {
            _client.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.HealthyCheck,
            });
            return _client.ReadServiceObjectAsync<InvokeResult>();
        }

        public InvokeResult GoCommit(RemoteClient tran)
        {
            _client.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.CommitTranaction,
                Header = tran.GetCommandHeader()
            });
            return _client.ReadServiceObject<InvokeResult>();
        }
        public Task<InvokeResult> GoCommitAsync(RemoteClient tran)
        {
            _client.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.CommitTranaction,
                Header = tran.GetCommandHeader()
            });
            return _client.ReadServiceObjectAsync<InvokeResult>();
        }

        public InvokeResult GoRollback(RemoteClient tran)
        {
            _client.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.RollbackTranaction,
                Header = tran.GetCommandHeader()
            });
            return _client.ReadServiceObject<InvokeResult>();
        }
        public Task<InvokeResult> GoRollbackAsync(RemoteClient tran)
        {
            _client.WriteServiceData(new InvokeCommand()
            {
                Type = (int)InvokeType.RollbackTranaction,
                Header = tran.GetCommandHeader()
            });
            return _client.ReadServiceObjectAsync<InvokeResult>();
        }

        public void AddClientToPool()
        {
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

        public void RetryTranaction(NetAddress proxyAddress, ClientServiceDetail serviceLocation, byte[] certData, string tranId, string tranFlag)
        {
            X509Certificate2 cert = null;
            if (certData != null)
            {
                cert = new X509Certificate2(certData);
            }
            _client = NetClientPool.CreateClient(proxyAddress, new NetAddress(serviceLocation.ServiceAddress, serviceLocation.Port, serviceLocation.UseSsl) { Certificate = cert, CertDomain = InvokingInfo.ServiceLocation.ServiceAddress });
            var command = new InvokeCommand()
            {
                Type = (int)InvokeType.RetryTranaction,
                Header = new Dictionary<string, string> {
                                    { "TranId" , tranId},
                                    {"TranFlag" , tranFlag }
                                }
            };

            _client.WriteServiceData(command);
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
