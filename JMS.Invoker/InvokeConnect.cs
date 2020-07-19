using JMS.Dtos;
using Microsoft.AspNetCore.Mvc;
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
        /// <summary>
        /// 重连次数
        /// </summary>
        internal int ReConnectCount = 0;
        public InvokingInformation InvokingInfo { get; private set; }

        public InvokeConnect(string serviceName , RegisterServiceLocation location)
        {
            this.InvokingInfo = new InvokingInformation();
            this.InvokingInfo.ServiceName = serviceName;
            this.InvokingInfo.ServiceLocation = location;
        }

       public void ReConnect(MicroServiceTransaction tran)
        {
            ReConnectCount++;
               NetClient = NetClientPool.CreateClient( tran.ProxyAddress, this.InvokingInfo.ServiceLocation.Host, this.InvokingInfo.ServiceLocation.Port, tran.ServiceClientCertificate);
        }

        public T Invoke<T>(string method,MicroServiceTransaction tran, params object[] parameters)
        {
            if(tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            this.InvokingInfo.MethodName = method;
            this.InvokingInfo.Parameters = parameters;

            var netclient = NetClientPool.CreateClient(tran.ProxyAddress, this.InvokingInfo.ServiceLocation.Host, this.InvokingInfo.ServiceLocation.Port, tran.ServiceClientCertificate);
            try
            {
                var cmd = new InvokeCommand()
                {
                    Header = tran.GetCommandHeader(),
                    Service = this.InvokingInfo.ServiceName,
                    Method = method,
                    Parameters = parameters.Length == 0 ? null :
                                    parameters.GetStringArrayParameters()
                };


                netclient.WriteServiceData(cmd);
                var result = netclient.ReadServiceObject<InvokeResult<T>>();
                if (result.Success == false)
                {
                    throw new RemoteException(result.Error);
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

                if(otherObj != null)
                    throw new ConvertException(otherObj.Data, $"无法将{otherObj.Data}实例化为{typeof(T).FullName}");

                throw ex;
            }
            catch (Exception)
            {
                netclient.Dispose();
                throw;
            }


        }
        public Task<T> InvokeAsync<T>(string method,  MicroServiceTransaction tran, params object[] parameter)
        {
            return Task.Run<T>(() => Invoke<T>(method,  tran, parameter));
        }

    }
}
