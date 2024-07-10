using Extreme.Net.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using JMS.Common.Net;
using Extreme.Net.Core.Proxy;
using JMS.IdentityModel.JWT.Authentication;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using JMS.Token;
using Way.Lib;
using System.Collections.Concurrent;
using System.Net.Sockets;
using JMS;
using System.Buffers;
using System.Net.Http.Headers;
using HttpClient = System.Net.Http.HttpClient;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;

namespace UnitTest
{
    [TestClass]
    public class HttpTest
    {
        [TestMethod]
        public void test()
        {
            startServer();
            runClient();

            while (true)
            {
                Thread.Sleep(20000);
            }
        }

        async void runClient()
        {
            await Task.Delay(1000);
            // 创建HttpClientHandler并禁用SSL证书验证
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, certificate, chain, sslPolicyErrors) => true
            };

            // 使用SocketsHttpHandler创建HttpClient
            using var httpClient = new HttpClient(handler);

            // 设置请求的HTTP版本（可选，HttpClient默认会尝试使用HTTP/2.0）
            httpClient.DefaultRequestVersion = new Version(2, 0);

            // 设置请求的标头，例如接受的内容类型
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // 发起请求
            var url = "https://127.0.0.1";
            var response = await httpClient.GetAsync(url);

            // 处理响应
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine(content);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
            }
        }

        async void startServer()
        {
            var cert = GenerateManualCertificate();
            var tcpListener = new TcpListener( IPAddress.Any, 443);
            tcpListener.Start();

            while (true)
            {
                var socket = await tcpListener.AcceptSocketAsync();
                handleSocket(socket, cert);
            }
        }

        async void handleSocket(Socket socket,X509Certificate2 cert)
        {
            var client = new NetClient(socket);
            await client.AsSSLServerWithProtocolAsync( new SslApplicationProtocol[] { 
                SslApplicationProtocol.Http2
            },
                cert, (s, s2,s3,s4) => true, System.Security.Authentication.SslProtocols.Tls12);


            if (client.SslApplicationProtocol == SslApplicationProtocol.Http2)
            {
                var headerSpan = ArrayPool<byte>.Shared.Rent(1024);
               

                try
                {
                    //"PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"  一共24个字节，在wireshark里称为magic，是一个纯HTTP/1消息，意思是以后的消息切换到HTTP/2
                    await client.ReadDataAsync(headerSpan, 0, 24);

                    while (true)
                    {
                        await client.ReadDataAsync(headerSpan, 0, 9);


                        var Length = (ushort)((headerSpan[0] << 16) | (headerSpan[1] << 8) | headerSpan[2]);
                        var type = (Http2FrameType)headerSpan[3];
                        var flags = headerSpan[4];
                        var streamId = (uint)((headerSpan[5] & 0x7F) << 24) | (headerSpan[6] << 16) | (headerSpan[7] << 8) | headerSpan[8];

                        await client.ReadDataAsync(headerSpan, 0, Length);

                        if (type == Http2FrameType.Headers)
                        {
                            //数据是HPack算法压缩的
                        }
                        var text = Encoding.UTF8.GetString(headerSpan, 0, Length);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(headerSpan);
                }
            }
        }
        public enum Http2FrameType : byte
        {
            // 定义HTTP/2.0帧类型
            Data = 0x0,
            Headers = 0x1,
            Priority = 0x2,
            _RST_STREAM = 0x3,
            SETTINGS = 0x4,
            PUSH_PROMISE = 0x5,
            PING = 0x6,
            GOAWAY = 0x7,
            WINDOW_UPDATE = 0x8,
            CONTINUATION = 0x9
        }
        static X509Certificate2 GenerateManualCertificate()
        {
            X509Certificate2 cert = null;
            var store = new X509Store("KestrelWebTransportCertificates", StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            if (store.Certificates.Count > 0)
            {
                cert = store.Certificates[^1];

                // rotate key after it expires
                if (DateTime.Parse(cert.GetExpirationDateString(), null) < DateTimeOffset.UtcNow)
                {
                    cert = null;
                }
            }
            if (cert == null)
            {
                // generate a new cert
                var now = DateTimeOffset.UtcNow;
                SubjectAlternativeNameBuilder sanBuilder = new();
                sanBuilder.AddDnsName("localhost");
                using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                CertificateRequest req = new("CN=localhost", ec, HashAlgorithmName.SHA256);
                // Adds purpose
                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
        {
            new("1.3.6.1.5.5.7.3.1") // serverAuth

        }, false));
                // Adds usage
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
                // Adds subject alternate names
                req.CertificateExtensions.Add(sanBuilder.Build());
                // Sign
                using var crt = req.CreateSelfSigned(now, now.AddDays(14)); // 14 days is the max duration of a certificate for this
                cert = new(crt.Export(X509ContentType.Pfx));

                // Save
                store.Add(cert);
            }
            store.Close();

            var hash = SHA256.HashData(cert.RawData);
            var certStr = Convert.ToBase64String(hash);
            //Console.WriteLine($"\n\n\n\n\nCertificate: {certStr}\n\n\n\n"); // <-- you will need to put this output into the JS API call to allow the connection
            return cert;
        }

    }

}
