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
        internal Way.Lib.NetStream NetClient;
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

       public void ReConnect()
        {
            ReConnectCount++;
               NetClient = new Way.Lib.NetStream(this.ServiceLocation.Host, this.ServiceLocation.Port);
            NetClient.ReadTimeout = 0;
        }

        public T Invoke<T>(string method,MicroServiceTransaction tran, params object[] parameter)
        {
            if(tran == null)
            {
                throw new ArgumentNullException("tran");
            }
            var netclient = new Way.Lib.NetStream(this.ServiceLocation.Host, this.ServiceLocation.Port);
            try
            {
#if DEBUG
                netclient.ReadTimeout = 0;
#else
            netclient.ReadTimeout = 16000;
#endif
                var cmd = new InvokeCommand()
                {
                    Header = tran.Header.Count > 0 ? tran.Header : null,
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
                    netclient.Dispose();
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
