
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Reflection.PortableExecutable;
using System.Buffers;
using System.Text;
using Org.BouncyCastle.Crypto.IO;
using JMS.Common.Net;

namespace JMS.Common
{
   
    /// <summary>
    /// 
    /// </summary>
    public class BaseNetClient : IDisposable
    {
        NetAddress _netaddr;
        public virtual NetAddress NetAddress => _netaddr;

        public Stream BaseStream => _innerStream;

        private Stream _innerStream;
        private Stream _pipeStream;
        public Stream InnerStream
        {
            get
            {
                return _pipeStream == null ? _innerStream : _pipeStream;
            }
            set
            {
                _innerStream = value;
                if(value == null)
                {
                    _PipeReader = null;
                    _pipeStream?.Dispose();
                    _pipeStream = null;
                }
                else
                {
                    _PipeReader = System.IO.Pipelines.PipeReader.Create(value, new System.IO.Pipelines.StreamPipeReaderOptions(null,-1,-1,true));
                    _pipeStream = new PipeLineStream(_PipeReader , _innerStream);
                }
            }
        }

        SslApplicationProtocol? _SslApplicationProtocol;
        public SslApplicationProtocol? SslApplicationProtocol => _SslApplicationProtocol;

        private System.IO.Pipelines.PipeReader _PipeReader;
        public System.IO.Pipelines.PipeReader PipeReader => _PipeReader;

        Socket _Socket;
        public Socket Socket
        {
            get => _Socket;
            set
            {
                _Socket = value;
                if(value != null)
                {
                    value.NoDelay = true;
                }
            }
        }
        public bool HasSocketException
        {
            get;
            internal set;
        }
        public System.Net.EndPoint RemoteEndPoint
        {
            get
            {
                return this.Socket.RemoteEndPoint;
            }
        }

        int _closedFlag = 0;



        public int ReadTimeout {
            get
            {
                if (this.Socket == null)
                    return 0;
                return this.Socket.ReceiveTimeout;
            }
            set
            {
                if (this.Socket != null)
                {
                    this.Socket.ReceiveTimeout = value;
                }
            }
        }

        public int WriteTimeout {
            get
            {
                return this.Socket.SendTimeout;
            }
            set
            {
                this.Socket.SendTimeout = value;
            }
        }



        public virtual void Dispose()
        {
            if (Interlocked.CompareExchange(ref _closedFlag , 1 , 0) == 0)
            {
                try
                {
                    _innerStream?.Dispose();
                    _innerStream = null;
                }
                catch
                {
                }

                try
                {
                    _PipeReader?.Complete();
                    _PipeReader = null;
                }
                catch 
                {

                }

                try
                {
                    _pipeStream?.Dispose();
                    _pipeStream = null;
                }
                catch 
                {

                }
                

                try
                {
                    Socket?.Dispose();
                }
                catch
                {
                }
                Socket = null;
            }
        }

        
        public BaseNetClient()
        {

        }

        private byte[] GetKeepAliveData()
        {
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
            BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes((uint)10000).CopyTo(inOptionValues, Marshal.SizeOf(dummy));//keep-alive间隔 10秒
            BitConverter.GetBytes((uint)2000).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);// 尝试间隔 2秒收不到回复，继续发问
            return inOptionValues;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="SocketClient"></param>
        public BaseNetClient(Socket SocketClient)
        {
            this.Socket = SocketClient;
            this.Socket.SendTimeout = 16000;
            this.Socket.ReceiveTimeout = 16000;
            this.Socket.ReceiveBufferSize = 1024 * 100;
            this.InnerStream = new InsideNetworkStream(this.Socket);
            try
            {
                this.Socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveData(), null);
            }
            catch {  }

        }

        public BaseNetClient(Stream stream)
        {
            this.InnerStream = stream;
        }


        /// <summary>
        /// 使用ssl协议作为客户端
        /// </summary>
        /// <param name="targetHost">证书对应的域名（有些证书要求必须输入正确的域名才能握手成功），没要求则可以传""</param>
        /// <param name="certificateValidationCallback"></param>
        public void AsSSLClient(string targetHost, RemoteCertificateValidationCallback certificateValidationCallback = null)
        {
            SslStream stream;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                stream = new SslStream(_innerStream, false, certificateValidationCallback);
            }
            else
            {
                stream = new SslStream(_innerStream);
            }
            try
            {
                stream.AuthenticateAsClient(targetHost);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }

            this.InnerStream = stream;
        }

        /// <summary>
        /// 使用ssl协议作为客户端
        /// </summary>
        /// <param name="targetHost">证书对应的域名（有些证书要求必须输入正确的域名才能握手成功），没要求则可以传""</param>
        /// <param name="certificateValidationCallback"></param>
        public async Task AsSSLClientAsync(string targetHost, RemoteCertificateValidationCallback certificateValidationCallback = null)
        {
            SslStream stream;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                stream = new SslStream(_innerStream, false, certificateValidationCallback);
            }
            else
            {
                stream = new SslStream(_innerStream);
            }
            try
            {
                await stream.AuthenticateAsClientAsync(targetHost);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }

            this.InnerStream = stream;
        }

        /// <summary>
        /// 使用ssl协议作为客户端
        /// </summary>
        /// <param name="targetHost">证书对应的域名（有些证书要求必须输入正确的域名才能握手成功），没要求则可以传""</param>
        /// <param name="clientCertificates">客户端证书</param>
        /// <param name="enabledSslProtocols"></param>
        /// <param name="certificateValidationCallback"></param>
        public void AsSSLClient(string targetHost, X509CertificateCollection clientCertificates, SslProtocols enabledSslProtocols, RemoteCertificateValidationCallback certificateValidationCallback = null)
        {
            SslStream stream;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                stream = new SslStream(_innerStream, false, certificateValidationCallback);
            }
            else
            {
                stream = new SslStream(_innerStream);
            }
            try
            {
                stream.AuthenticateAsClient(targetHost, clientCertificates, enabledSslProtocols, false);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }

            this.InnerStream = stream;
        }

        /// <summary>
        /// 使用ssl协议作为客户端
        /// </summary>
        /// <param name="targetHost">证书对应的域名（有些证书要求必须输入正确的域名才能握手成功），没要求则可以传""</param>
        /// <param name="clientCertificates">客户端证书</param>
        /// <param name="enabledSslProtocols"></param>
        /// <param name="certificateValidationCallback"></param>
        public async Task AsSSLClientAsync(string targetHost, X509CertificateCollection clientCertificates, SslProtocols enabledSslProtocols, RemoteCertificateValidationCallback certificateValidationCallback = null)
        {
            SslStream stream;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                stream = new SslStream(_innerStream, false, certificateValidationCallback);
            }
            else
            {
                stream = new SslStream(_innerStream);
            }
            try
            {
                await stream.AuthenticateAsClientAsync(targetHost, clientCertificates, enabledSslProtocols, false);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }

            this.InnerStream = stream;
        }

        /// <summary>
        /// 使用ssl协议作为服务器端
        /// </summary>
        /// <param name="ssl"></param>
        /// <param name="protocol"></param>
        public void AsSSLServer(X509Certificate2 ssl, bool clientCertificateRequired, RemoteCertificateValidationCallback remoteCertificateValidationCallback, SslProtocols protocol = SslProtocols.Tls)
        {
            SslStream sslStream = new SslStream(_innerStream, false, remoteCertificateValidationCallback);
            try
            {
                sslStream.AuthenticateAsServer(ssl, clientCertificateRequired, protocol, false);
            }
            catch (Exception)
            {
                sslStream.Dispose();
                throw;
            }

            this.InnerStream = sslStream;
        }
        /// <summary>
        /// 使用ssl协议作为服务器端
        /// </summary>
        /// <param name="ssl"></param>
        /// <param name="protocol"></param>
        public async Task AsSSLServerAsync(X509Certificate2 ssl, bool clientCertificateRequired, RemoteCertificateValidationCallback remoteCertificateValidationCallback, SslProtocols protocol = SslProtocols.Tls)
        {
            SslStream sslStream = new SslStream(_innerStream, false ,remoteCertificateValidationCallback);
            try
            {
                await sslStream.AuthenticateAsServerAsync(ssl, clientCertificateRequired, protocol, false);
            }
            catch (Exception)
            {
                sslStream.Dispose();
                throw;
            }

            this.InnerStream = sslStream;
        }

        /// <summary>
        /// 使用ssl协议作为服务器端，并向客户端声明自己支持的协议，
        /// 连接成功后，通过SslApplicationProtocol属性获取最终和客户端敲定的协议
        /// </summary>
        /// <param name="supportAppProtocols"></param>
        /// <param name="ssl"></param>
        /// <param name="remoteCertificateValidationCallback"></param>
        /// <param name="protocol"></param>
        public async Task AsSSLServerWithProtocolAsync(SslApplicationProtocol[] supportAppProtocols, X509Certificate2 ssl,bool clientCertificateRequired, RemoteCertificateValidationCallback remoteCertificateValidationCallback, SslProtocols protocol = SslProtocols.Tls)
        {
            SslStream sslStream = new SslStream(_innerStream, false, remoteCertificateValidationCallback);
            try
            {
                await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = ssl,
                    ClientCertificateRequired = clientCertificateRequired,
                    RemoteCertificateValidationCallback = remoteCertificateValidationCallback,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    EnabledSslProtocols = protocol,
                    ApplicationProtocols = new List<SslApplicationProtocol>(supportAppProtocols)
                }, CancellationToken.None);

                _SslApplicationProtocol = sslStream.NegotiatedApplicationProtocol;
            }
            catch (Exception)
            {
                sslStream.Dispose();
                throw;
            }
            this.InnerStream = sslStream;
        }

        public virtual void Connect(NetAddress addr)
        {
            EndPoint endPoint;
            if (IPAddress.TryParse(addr.Address, out IPAddress ip))
            {
                endPoint = new IPEndPoint(ip, addr.Port);
            }
            else
            {
                var ipaddresses = Dns.GetHostAddresses(addr.Address);
                var ipv4address = ipaddresses.FirstOrDefault(m => m.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4address == null)
                    ipv4address = ipaddresses[0];

                endPoint = new IPEndPoint(ipv4address, addr.Port);
            }

            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = 16000;
            socket.ReceiveTimeout = 16000;
            try
            {
                socket.Connect(endPoint);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            this.Socket = socket;
            this.InnerStream = new InsideNetworkStream(socket);
            try
            {
                this.Socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveData(), null);
            }
            catch { }

            _netaddr = addr;
        }

        public virtual async Task ConnectAsync(NetAddress addr)
        {
            EndPoint endPoint;
            if (IPAddress.TryParse(addr.Address, out IPAddress ip))
            {
                endPoint = new IPEndPoint(ip, addr.Port);
            }
            else
            {
                var ipaddresses = await Dns.GetHostAddressesAsync(addr.Address);
                var ipv4address = ipaddresses.FirstOrDefault(m => m.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4address == null)
                    ipv4address = ipaddresses[0];

                endPoint = new IPEndPoint(ipv4address, addr.Port);
            }

            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = 16000;
            socket.ReceiveTimeout = 16000;
            try
            {
                await socket.ConnectAsync(endPoint);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            this.Socket = socket;
            this.InnerStream = new InsideNetworkStream(socket);
            try
            {
                this.Socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveData(), null);
            }
            catch { }

            _netaddr = addr;
        }

        public void Write(byte[] data)
        {
            this.InnerStream.Write(data);
        }

        public void Write(int data)
        {
            this.InnerStream.Write(BitConverter.GetBytes(data));
        }
        public void Write(long data)
        {
            this.InnerStream.Write(BitConverter.GetBytes(data));
        }


        public Task<string> ReadLineAsync()
        {
            return ReadLineAsync(0);
        }

        public async Task<string> ReadLineAsync(int maxLength)
        {
            ReadResult ret;
            SequencePosition? position;
            string line;
            byte n = (byte)'\n';
            ReadOnlySequence<byte> block;
            while (true)
            {
                ret = await _PipeReader.ReadAsync();
                var buffer = ret.Buffer;
                if (ret.IsCompleted)
                {
                    if (buffer.Length > 0)
                    {
                        _PipeReader.AdvanceTo(buffer.End);
                    }
                    throw new SocketException();
                }
               

                position = buffer.PositionOf(n);
                if (position != null)
                {
                    block = buffer.Slice(0, position.Value);

                    line = block.GetString().Trim();

                    // 告诉PipeReader已经处理多少缓冲
                    _PipeReader.AdvanceTo(buffer.GetPosition(1, position.Value));
                    return line;
                }
                else
                {
                   

                    if (maxLength > 0 && buffer.Length > maxLength)
                        throw new SizeLimitException("line is too long");

                    // 告诉PipeReader已经处理多少缓冲
                    _PipeReader.AdvanceTo(buffer.Start,buffer.End);
                }               

            }
        }


        public void WriteLine(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes($"{text}\r\n");
            this.InnerStream.Write(buffer, 0, buffer.Length);
        }

    }



}
