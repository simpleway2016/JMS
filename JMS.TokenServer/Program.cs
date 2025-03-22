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
using System.Linq;
using System.Threading;
using JMS.Common.Net;
using System.IO;
using JMS.Common;
using System.Security.Authentication;

namespace JMS.TokenServer
{
    class Program
    {
        static ILogger<Program> Logger;
        static string[] key;
        static byte[] data;
        static X509Certificate2 ServerCert;
        static SslProtocols SslProtocol;
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
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 10, Environment.ProcessorCount * 10);

            var builder = new ConfigurationBuilder();

            CommandArgParser cmdArg = new CommandArgParser(args);
            var appSettingPath = cmdArg.TryGetValue<string>("-s");

            if (appSettingPath == null)
                appSettingPath = "appsettings.json";
            if (appSettingPath == "share")
            {
                appSettingPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                appSettingPath = Path.Combine(appSettingPath, "jms.tokenserver");
                if (Directory.Exists(appSettingPath) == false)
                {
                    Directory.CreateDirectory(appSettingPath);
                }
                appSettingPath = Path.Combine(appSettingPath, "appsettings.json");
                if (File.Exists(appSettingPath) == false)
                {
                    File.Copy("./appsettings.json", appSettingPath);
                }
            }

            builder.AddJsonFile(appSettingPath, optional: true, reloadOnChange: true);
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

                var sslProtocol = configuration.GetSection("SSL:SslProtocols").Get<SslProtocols?>();
                if (sslProtocol == null)
                    sslProtocol = SslProtocols.None;
                SslProtocol = sslProtocol.Value;
            }

            var listener = new JMS.ServerCore.MulitTcpListener(port, Logger);
            listener.Connected += Listener_Connected;
            Logger?.LogInformation($"配置文件：{appSettingPath}");
            Logger?.LogInformation($"Token server started,port：{port}");
            listener.Run();
        }

        private static void Listener_Connected(object sender, Socket socket)
        {
            Task.Run(() => onSocket(socket));
        }

        static bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (AcceptCertHash != null && AcceptCertHash.Length > 0 && AcceptCertHash.Contains(certificate.GetCertHashString()) == false)
            {
                return false;
            }
            return true;
        }
        static async void onSocket(Socket socket)
        {
            NetClient client = null;
            try
            {
                client = new NetClient(socket);
                client.ReadTimeout = 0;
                if (ServerCert != null)
                {
                    await client.AsSSLServerAsync(ServerCert, false,new RemoteCertificateValidationCallback(RemoteCertificateValidationCallback), SslProtocol);
                }
               
                var flag = await client.ReadIntAsync();
                if (flag == 0 || flag == 1179010630)//健康检查
                {
                    client.Write(Encoding.UTF8.GetBytes("ok"));
                }
                else if(flag == 1)//get key
                { 
                    //data里面前面四个字节包含了长度,所以不用先Write(数据长度)
                    client.Write(data);
                    await client.ReadIntAsync();
                }
                else if (flag == 2) //disable token
                {
                    var expireTimeLong = client.ReadLong();//utc过期时间
                    var len = client.ReadInt();
                    var data = new byte[len];
                    await client.ReadDataAsync(data, 0, len);

                    var token = System.Text.Encoding.UTF8.GetString(data);
                    _ClientManager.DisableToken(token, expireTimeLong);
                    client.Write(true);
                }
                else if (flag == 888)//for test
                {
                    client.Write(4);
                    client.Write(888);
                    await client.ReadIntAsync();
                }
                else if(flag == 999)
                { 
                    //data里面前面四个字节包含了长度,所以不用先Write(数据长度)
                    client.Write(data);
                    _ClientManager.AddClient(client).Handle();
                }
            }
            catch(SocketException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;

                if (ex is SocketException)
                    return;

                Logger.LogError(ex, ex.Message);
            }
            finally
            {
                client?.Dispose();
            }
        }
    }
}
