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

using System.Collections.Concurrent;
using System.Net.Sockets;
using JMS;
using System.Buffers;
using System.Net.Http.Headers;
using HttpClient = System.Net.Http.HttpClient;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;
using JMS.Common.Collections;

namespace UnitTest
{
    [TestClass]
    public class HttpV2Test
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
            var headerTable = new Dictionary<int, (string HeaderName, string HeaderValue)>
        {
            { 1, (":authority", "") },
            { 2, (":method", "GET") },
            { 3, (":method", "POST") },
            { 4, (":path", "/") },
            { 5, (":path", "/index.html") },
            { 6, (":scheme", "http") },
            { 7, (":scheme", "https") },
            { 8, (":status", "200") },
            { 9, (":status", "204") },
            { 10, (":status", "206") },
            { 11, (":status", "304") },
            { 12, (":status", "400") },
            { 13, (":status", "404") },
            { 14, (":status", "500") },
            { 15, ("accept-charset", "") },
            { 16, ("accept-encoding", "gzip, deflate") },
            { 17, ("accept-language", "") },
            { 18, ("accept-ranges", "") },
            { 19, ("accept", "") },
            { 20, ("access-control-allow-origin", "") },
            { 21, ("age", "") },
            { 22, ("allow", "") },
            { 23, ("authorization", "") },
            { 24, ("cache-control", "") },
            { 25, ("content-disposition", "") },
            { 26, ("content-encoding", "") },
            { 27, ("content-language", "") },
            { 28, ("content-length", "") },
            { 29, ("content-location", "") },
            { 30, ("content-range", "") },
            { 31, ("content-type", "") },
            { 32, ("cookie", "") },
            { 33, ("date", "") },
            { 34, ("etag", "") },
            { 35, ("expect", "") },
            { 36, ("expires", "") },
            { 37, ("from", "") },
            { 38, ("host", "") },
            { 39, ("if-match", "") },
            { 40, ("if-modified-since", "") },
            { 41, ("if-none-match", "") },
            { 42, ("if-range", "") },
            { 43, ("if-unmodified-since", "") },
            { 44, ("last-modified", "") },
            { 45, ("link", "") },
            { 46, ("location", "") },
            { 47, ("max-forwards", "") },
            { 48, ("proxy-authenticate", "") },
            { 49, ("proxy-authorization", "") },
            { 50, ("range", "") },
            { 51, ("referer", "") },
            { 52, ("refresh", "") },
            { 53, ("retry-after", "") },
            { 54, ("server", "") },
            { 55, ("set-cookie", "") },
            { 56, ("strict-transport-security", "") },
            { 57, ("transfer-encoding", "") },
            { 58, ("user-agent", "") },
            { 59, ("vary", "") },
            { 60, ("via", "") },
            { 61, ("www-authenticate", "") }
        };

            var client = new NetClient(socket);
            await client.AsSSLServerWithProtocolAsync( new SslApplicationProtocol[] { 
                SslApplicationProtocol.Http2
            },
                cert, false, (s, s2,s3,s4) => true, System.Security.Authentication.SslProtocols.Tls12);


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

                            var headers = new IgnoreCaseDictionary();
                            //数据是HPack算法压缩的
                            byte data;

                            for (int i = 0; i < Length;)
                            {
                                data = headerSpan[i];

#if DEBUG
                                var str = Convert.ToString((int)data, 2);
#endif
                                if ((data & 0b10000000) == 0b10000000)
                                {
                                    //存在静态表
                                    var index = data & 0b01111111;
                                    var headerItem = headerTable[index];
                                    headers[headerItem.HeaderName] = headerItem.HeaderValue;
                                    i++;
                                }
                                else if (data == 0b00001111)
                                {
                                    //索引值太大，需要再读一个字节
                                    i++;

                                    //Key 被索引，value 未索引且不允许保存
                                    data = headerSpan[i];
                                    var index = data & 0b01111111;
                                    index += 0b00001111;
                                    var headerItem = headerTable[index];
                                    i++;
                                    data = headerSpan[i];
                                    var len = data & 0b01111111;
                                    i++;
                                    var value = Encoding.UTF8.GetString(headerSpan, i, len);
                                    i += len;

                                    headers[headerItem.HeaderName] =value;
                                }
                                else if((data & 0b11110000) == 0)
                                {
                                    //Key 被索引，value 未索引且不允许保存
                                    var index = data & 0b01111111;
                                    var headerItem = headerTable[index];
                                    i++;
                                    data = headerSpan[i];
                                    var len = data & 0b01111111;
                                    i++;
                                    var value = Encoding.UTF8.GetString( headerSpan , i , len );
                                    i += len;

                                    headers[headerItem.HeaderName] =  value;
                                }

                            }
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
