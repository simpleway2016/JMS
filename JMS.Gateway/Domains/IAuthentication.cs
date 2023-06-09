using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Domains
{
    internal interface IAuthentication
    {
        /// <summary>
        /// 开始验证身份
        /// </summary>
        /// <param name="netclient"></param>
        /// <param name="cmd"></param>
        /// <returns>验证通过返回true，失败返回false</returns>
        Task<bool> Verify(NetClient netclient, GatewayCommand cmd);
    }

    class Authentication : IAuthentication
    {
        ErrorUserMarker _errorUserMarker;
        IConfiguration _configuration;
        public Authentication(IConfiguration configuration, ErrorUserMarker errorUserMarker)
        {
            this._errorUserMarker = errorUserMarker;
            this._configuration = configuration;

        }
        void outputNeedLogin(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.IsHttp)
            {
                netclient.OutputHttp401();
            }
            else
            {
                netclient.WriteServiceData(new RegisterServiceInfo[] { new RegisterServiceInfo() {
                        ServiceList = new ServiceDetail[]{ new ServiceDetail { Name = "username , password error" } }
                    } });
            }
        }
        void outputBlackList(NetClient netclient, GatewayCommand cmd)
        {
            if (cmd.IsHttp)
            {
                netclient.OutputHttp401();
            }
            else
            {
                netclient.WriteServiceData(new RegisterServiceInfo[] { new RegisterServiceInfo() {
                        ServiceList = new ServiceDetail[]{ new ServiceDetail { Name = "username , password error. in black list" } }
                    } });
            }
        }

        public async Task<bool> Verify(NetClient netclient, GatewayCommand cmd)
        {
            var userInfos = _configuration.GetSection("Http:Users").Get<UserInfo[]>();
            if (userInfos != null && userInfos.Length > 0)
            {
                var ip = ((IPEndPoint)netclient.RemoteEndPoint).Address.ToString();
                if (_errorUserMarker.CheckUserIp(ip) == false)
                {
                    outputBlackList(netclient, cmd);
                    return false;
                }

                //检验身份
                string username, pwd;
                cmd.Header.TryGetValue("UserName", out username);
                if (string.IsNullOrWhiteSpace(username))
                {
                    outputNeedLogin(netclient, cmd);
                    return false;
                }
                cmd.Header.TryGetValue("Password", out pwd);

                if (userInfos.Any(m => string.Equals(m.UserName, username, StringComparison.OrdinalIgnoreCase) && m.Password == pwd) == false)
                {
                    _errorUserMarker.Error(ip);
                    outputNeedLogin(netclient, cmd);
                    return false;
                }

                _errorUserMarker.Clear(ip);
            }
            return true;
        }
    }
}
