using JMS.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Linq;

namespace JMS.Proxy
{
    class Connect : IDisposable
    {
        NetClient _client;
        NetClient _targetClient;
        Socks5Server _proxy;
        Header _header;
        ILogger<Connect> _logger;
        public Connect(NetClient client, Socks5Server proxy)
        {
            _client = client;
            _proxy = proxy;
            _logger = proxy.ServiceProvider.GetService<ILogger<Connect>>();
            _client.ReadTimeout = 0;
        }

        public void Dispose()
        {
            _targetClient?.Dispose();
            _client.Dispose();
        }

        /// <summary>
        /// 处理握手
        /// </summary>
        private async Task<bool> shakeHands()
        {
            byte[] buffer = new byte[256];
            byte method = 0xFF; //命令不支持
            await _client.ReadDataAsync(buffer, 0, 2);

            if (buffer[1] > 0)
            {
                //取得认证方法列
                await _client.ReadDataAsync(buffer, 0, (int)buffer[1]);
            }

            //不需要验证身份
            method = 0x00;
            buffer[0] = 0x05;
            buffer[1] = method;
            //发送应答
            _client.InnerStream.Write(buffer , 0 , 2);

            _header = new Header();
            //取前4字节
            await _client.ReadDataAsync(buffer, 0, 4);


            byte rep = 0x07;            //不支持的命令

            //判断地址类型
            switch (buffer[3])
            {
                case 0x01:
                    //IPV4
                    var data = new byte[4];
                    await _client.ReadDataAsync(data, 0, data.Length);
                    _header.Address = new IPAddress(data).ToString();
                    break;
                case 0x03:
                    //域名
                    int len = _client.InnerStream.ReadByte();
                    if (len < 0)
                        throw new SocketException();
                    await _client.ReadDataAsync(buffer, 0, len);

                    _header.IsDomain = true;
                    //取得域名地址
                    _header.Address = Encoding.ASCII.GetString(buffer , 0 ,len);

                    break;
                case 0x04:
                    //IPV6;
                    var data6 = new byte[16];
                    await _client.ReadDataAsync(data6, 0, data6.Length);
                    _header.Address = new IPAddress(data6).ToString();
                    break;
                default:
                    rep = 0x08; //不支持的地址类型
                    break;
            }

            if (rep == 0x07)
            {
                //取得端口号
                await _client.ReadDataAsync(buffer, 0, 2);
                buffer[3] = buffer[0];  //反转端口值
                buffer[0] = buffer[1];
                buffer[1] = buffer[3];
                _header.Port = BitConverter.ToUInt16(buffer, 0);
                rep = 0x00;
            }

            var result = true;
            if(_proxy.WhiteList != null && _proxy.WhiteList.Any(m=>m.Address == _header.Address && m.Port == _header.Port) == false)
            {
                rep = 0x08;
                result = false;
            }

            //输出应答
           
            for (int i = 0; i < 10; i++)
            {
                buffer[i] = 0;
            }
            buffer[0] = (0x05);
            buffer[1] = (rep);
            buffer[2] = (0x00);
            buffer[3] = (0x01);

           _client.InnerStream.Write(buffer, 0, 10);
            return result;
        }


        public async Task Start()
        {
            if(await shakeHands() == false)
            {
                await Task.Delay(2000);
                return;
            }
            _logger.LogDebug($"连接{_header.Address}:{_header.Port}");
            _targetClient = new NetClient();
            await _targetClient.ConnectAsync(new NetAddress(_header.Address, _header.Port));
            _targetClient.ReadTimeout = 0;
            _client.ReadTimeout = 0;

            _targetClient.ReadAndSendForLoop(_client);

            await _client.ReadAndSendForLoop(_targetClient);


        }

    }

    internal class Header
    {
        public string Address { get; set; }
        public ushort Port { get; set; }
        public bool IsDomain { get; set; }
    }
}
