using JMS.Dtos;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS
{
    class InvokeConnect
    {
        public RegisterServiceLocation ServiceLocation { get; }
        internal NetClient NetClient;
        /// <summary>
        /// 重连次数
        /// </summary>
        internal int ReConnectCount = 0;

        public string ServiceName
        {
            get;
        }
        public InvokeConnect(string serviceName , RegisterServiceLocation location)
        {
            ServiceName = serviceName;
            this.ServiceLocation = location;
        }

       public void ReConnect(MicroServiceTransaction tran)
        {
            ReConnectCount++;
               NetClient = NetClientPool.CreateClient(this.ServiceLocation.Host, this.ServiceLocation.Port, tran.ServiceClientCertificate);
        }

        public T Invoke<T>(string method,MicroServiceTransaction tran, params object[] parameter)
        {
            if(tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            var netclient = NetClientPool.CreateClient(this.ServiceLocation.Host, this.ServiceLocation.Port, tran.ServiceClientCertificate);
            try
            {
                var cmd = new InvokeCommand()
                {
                    Header = tran.GetCommandHeader(),
                    Service = ServiceName,
                    Method = method,
                    Parameters = parameter.Length == 0 ? null :
                                    parameter.GetStringArrayParameters()
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
