using JMS.Dtos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    public class InvokingInformation
    {
        public RegisterServiceLocation ServiceLocation { get; internal set; }
        public string ServiceName
        {
            get;
            internal set;
        }
        public string MethodName { get; internal set; }
        public object[] Parameters { get; internal set; }
    }
    class InvokeConnect
    {
       
        internal NetClient NetClient;

        public InvokingInformation InvokingInfo { get; private set; }
        public InvokeCommand Command { get; private set; }
        public Invoker Invoker { get; private set; }
        public InvokeConnect(string serviceName , RegisterServiceLocation location, Invoker invoker)
        {
            this.InvokingInfo = new InvokingInformation();
            this.InvokingInfo.ServiceName = serviceName;
            this.InvokingInfo.ServiceLocation = location;
            Invoker = invoker;
        }


        public T Invoke<T>(string method, IRemoteClient tran, params object[] parameters)
        {
            return this.Invoke<T>(method, tran, tran.GetCommandHeader(), parameters);
        }

        public T Invoke<T>(string method, IRemoteClient tran,Dictionary<string,string> headers , params object[] parameters)
        {
            if (tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            this.InvokingInfo.MethodName = method;
            this.InvokingInfo.Parameters = parameters;

            var netclient = NetClientPool.CreateClient(tran.ProxyAddress, this.InvokingInfo.ServiceLocation.ServiceAddress, this.InvokingInfo.ServiceLocation.Port, tran.ServiceClientCertificate);
            try
            {
                netclient.ReadTimeout = tran.Timeout;
                this.Command = new InvokeCommand()
                {
                    Header = headers,
                    Service = this.InvokingInfo.ServiceName,
                    Method = method,
                    Parameters = parameters.Length == 0 ? null :
                                    parameters.GetStringArrayParameters()
                };


                netclient.WriteServiceData(this.Command);
                var result = netclient.ReadServiceObject<InvokeResult<T>>();
                if (result.Success == false)
                {
                    throw new RemoteException(tran.TransactionId, result.Error);
                }
                NetClient = netclient;

                if (result.SupportTransaction)
                    tran.AddConnect(this);
                else
                {
                    NetClientPool.AddClientToPool(netclient);
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
                netclient.Dispose();
                throw;
            }


        }
        public Task<T> InvokeAsync<T>(string method,  IRemoteClient tran, params object[] parameter)
        {
            var headers = tran.GetCommandHeader();
            return Task.Run<T>(() => Invoke<T>(method,  tran,headers, parameter));
        }

    }
}
