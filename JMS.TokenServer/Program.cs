using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;
using System.Linq;

namespace JMS.TokenServer
{
    class Program
    {
        static ILogger<Program> Logger;
        static string[] key;
        static byte[] data;
        static X509Certificate2 ServerCert;
        static string[] AcceptCertHash;
        static ClientManager _ClientManager = new ClientManager();
        static string GetRandomString(int length)
        {
            byte[] b = new byte[4];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(b);
            Random r = new Random(BitConverter.ToInt32(b, 0));
            string s = null, str = "";
            str += "0123456789";
            str += "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            str += "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
            for (int i = 0; i < length; i++)
            {
                s += str.Substring(r.Next(0, str.Length - 1), 1);
            }
            return s;
        }

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            var configuration = builder.Build();

            var port = configuration.GetValue<int>("Port");

            ServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
                loggingBuilder.AddConsole(); // 将日志输出到控制台
            });

            var serviceProvider = services.BuildServiceProvider();
            Logger = serviceProvider.GetService<ILogger<Program>>();

            Random random = new Random();
            key = new string[2];
            key[0] = GetRandomString(32);
            key[1] = GetRandomString(random.Next(36 , 66));

            var strByte = Encoding.UTF8.GetBytes(key.ToJsonString());
            List<byte> bs = new List<byte>();
            bs.AddRange(BitConverter.GetBytes(strByte.Length));
            bs.AddRange(strByte);
            data = bs.ToArray();

            //SSL
            var certPath = configuration.GetValue<string>("SSL:Cert");
            if (!string.IsNullOrEmpty(certPath))
            {
                ServerCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath, configuration.GetValue<string>("SSL:Password"));
                AcceptCertHash = configuration.GetSection("SSL:AcceptCertHash").Get<string[]>();
            }

            TcpListener listener = new TcpListener(port);
            listener.Start();
            Console.WriteLine($"Token server started,port：{port}");
            while(true)
            {
                var socket = listener.AcceptSocket();
                Task.Run(()=>onSocket(socket));
            }
        }
        static bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (AcceptCertHash != null && AcceptCertHash.Length > 0 && AcceptCertHash.Contains(certificate.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }
        static void onSocket(Socket socket)
        {
            Way.Lib.NetStream client = null;
            try
            {
                client = new Way.Lib.NetStream(socket);
                client.ReadTimeout = 0;
                if (ServerCert != null)
                {
                    var sslts = new SslStream(client.InnerStream, false, new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback));
                    sslts.AuthenticateAsServer(ServerCert, true, NetClient.SSLProtocols, false);
                    client.InnerStream = sslts;
                }
               
                var flag = client.ReadInt();
                if (flag == 0 || flag == 1179010630)//健康检查
                {
                    client.Write(Encoding.UTF8.GetBytes("ok"));
                }
                else if(flag == 1)//get key
                { 
                    //data里面前面四个字节包含了长度,所以不用先Write(数据长度)
                    client.Write(data);
                    client.ReadInt();
                }
                else if (flag == 2) //disable token
                {
                    var expireTimeLong = client.ReadLong();//utc过期时间
                    var len = client.ReadInt();                    
                    var token = System.Text.Encoding.UTF8.GetString( client.ReceiveDatas(len));
                    _ClientManager.DisableToken(token, expireTimeLong);
                    client.Write(true);
                }
                else if (flag == 998)//for test
                {
                    client.Write(4);
                    client.Write(888);
                    client.ReadInt();
                }
                else if(flag == 999)
                {
                     _ClientManager.AddClient(client).Handle();
                }
            }
            catch(SocketException)
            {

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
            }
            finally
            {
                client?.Dispose();
            }
        }
    }
}
