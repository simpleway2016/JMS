
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

namespace JMS.Common
{
   
    /// <summary>
    /// 
    /// </summary>
    public class BaseNetClient : IDisposable
    {
        public string Address { get; protected set; }
        public int Port { get; protected set; }
        private Stream _stream;
        public Stream InnerStream
        {
            get
            {
                return _stream;
            }
            set
            {
                _stream = value;
            }
        }
        public Socket Socket
        {
            get;
            set;
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
        private bool m_Active;
        private System.Text.Encoding code = System.Text.Encoding.UTF8;
        bool _closed;

        private const int dataBuffer = 1024;

        public System.Text.Encoding Encoding
        {
            get
            {
                return code;
            }
            set
            {
                code = value;
            }
        }

        private System.Text.Encoding _ErrorEncoding = System.Text.Encoding.UTF8;
        public System.Text.Encoding ErrorEncoding
        {
            get
            {
                return _ErrorEncoding;
            }
            set
            {
                _ErrorEncoding = value;
            }
        }

        public int ReadTimeout {
            get
            {
                return this.Socket.ReceiveTimeout;
            }
            set
            {
                this.Socket.ReceiveTimeout = value;
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
            if (!_closed)
            {
                _closed = true;
                try
                {
                    _stream?.Dispose();
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
            this._stream = new NetworkStream(this.Socket);
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
        /// 
        /// </summary>
        /// <param name="SocketClient"></param>
        /// <param name="ssl">ssl证书</param>
        /// <param name="sslProtocols"></param>
        [Obsolete("use AsSSLServer")]
        public BaseNetClient(Socket SocketClient, X509Certificate2 ssl, SslProtocols sslProtocols)
        {
            this.Socket = SocketClient;
            SslStream sslStream = new SslStream(new NetworkStream(this.Socket), false);
            try
            {
               
                sslStream.AuthenticateAsServer(ssl, false, sslProtocols, true);
                this._stream = sslStream;

                this.Socket.SendTimeout = 16000;
                this.Socket.ReceiveTimeout = 16000;
                this.Socket.ReceiveBufferSize = 1024 * 100;
            }
            catch(Exception ex)
            {
                sslStream.Close();
                SocketClient.Close();
                throw ex;
            }
          

            try
            {
                this.Socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveData(), null);
            }
            catch {  }
        }

        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        /// <summary>
        /// 使用ssl协议作为客户端
        /// </summary>
        /// <param name="targetHost">证书对应的域名（有些证书要求必须输入正确的域名才能握手成功），没要求则可以传""</param>
        /// <param name="certificateValidationCallback"></param>
        public void AsSSLClient(string targetHost, RemoteCertificateValidationCallback certificateValidationCallback = null)
        {
            SslStream client;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                client = new SslStream(_stream, false, certificateValidationCallback);
            }
            else
            {
                client = new SslStream(_stream);
            }
            client.AuthenticateAsClient(targetHost);

            _stream = client;
        }

        /// <summary>
        /// 使用ssl协议作为客户端
        /// </summary>
        /// <param name="targetHost">证书对应的域名（有些证书要求必须输入正确的域名才能握手成功），没要求则可以传""</param>
        /// <param name="certificateValidationCallback"></param>
        public async Task AsSSLClientAsync(string targetHost, RemoteCertificateValidationCallback certificateValidationCallback = null)
        {
            SslStream client;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                client = new SslStream(_stream, false, certificateValidationCallback);
            }
            else
            {
                client = new SslStream(_stream);
            }
            await client.AuthenticateAsClientAsync(targetHost);

            _stream = client;
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
            SslStream client;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                client = new SslStream(_stream, false, certificateValidationCallback);
            }
            else
            {
                client = new SslStream(_stream);
            }
            client.AuthenticateAsClient(targetHost, clientCertificates,enabledSslProtocols,false);

            _stream = client;
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
            SslStream client;
            if (certificateValidationCallback != null || ServicePointManager.ServerCertificateValidationCallback != null)
            {
                if (certificateValidationCallback == null)
                    certificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;

                client = new SslStream(_stream, false, certificateValidationCallback);
            }
            else
            {
                client = new SslStream(_stream);
            }
            await client.AuthenticateAsClientAsync(targetHost, clientCertificates, enabledSslProtocols, false);

            _stream = client;
        }

        /// <summary>
        /// 使用ssl协议作为服务器端
        /// </summary>
        /// <param name="ssl"></param>
        /// <param name="protocol"></param>
        public void AsSSLServer(X509Certificate2 ssl , RemoteCertificateValidationCallback remoteCertificateValidationCallback, SslProtocols protocol = SslProtocols.Tls)
        {
            SslStream sslStream = new SslStream(_stream, false, remoteCertificateValidationCallback);
            sslStream.AuthenticateAsServer(ssl, true, protocol, false);

            _stream = sslStream;
        }
        /// <summary>
        /// 使用ssl协议作为服务器端
        /// </summary>
        /// <param name="ssl"></param>
        /// <param name="protocol"></param>
        public async Task AsSSLServerAsync(X509Certificate2 ssl, RemoteCertificateValidationCallback remoteCertificateValidationCallback, SslProtocols protocol = SslProtocols.Tls)
        {
            SslStream sslStream = new SslStream(_stream, false ,remoteCertificateValidationCallback);
            await sslStream.AuthenticateAsServerAsync(ssl, true, protocol, false);

            _stream = sslStream;
        }

        public void Connect(NetAddress addr)
        {
            this.Connect(addr.Address, addr.Port);
        }

        public Task ConnectAsync(NetAddress addr)
        {
            return this.ConnectAsync(addr.Address, addr.Port);
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Connect(string address, int port)
        {
            EndPoint endPoint;
            if (IPAddress.TryParse(address, out IPAddress ip))
            {
                endPoint = new IPEndPoint(ip, port);
            }
            else
            {
                var ipaddresses = Dns.GetHostAddresses(address);
                var ipv4address = ipaddresses.FirstOrDefault(m => m.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4address == null)
                    ipv4address = ipaddresses[0];

                endPoint = new IPEndPoint(ipv4address, port);
            }

            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = 16000;
            socket.ReceiveTimeout = 16000;
            socket.Connect(endPoint);

            this.Socket = socket;
            this._stream = new NetworkStream(socket);
            try
            {
                this.Socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveData(), null);
            }
            catch { }

            this.Address = address;
            this.Port = port;
        }

        public virtual async Task ConnectAsync(string address, int port)
        {
            EndPoint endPoint;
            if (IPAddress.TryParse(address, out IPAddress ip))
            {
                endPoint = new IPEndPoint(ip, port);
            }
            else
            {
                var ipaddresses = await Dns.GetHostAddressesAsync(address);
                var ipv4address = ipaddresses.FirstOrDefault(m => m.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4address == null)
                    ipv4address = ipaddresses[0];

                endPoint = new IPEndPoint(ipv4address, port);
            }

            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = 16000;
            socket.ReceiveTimeout = 16000;
            await socket.ConnectAsync(endPoint);

            this.Socket = socket;
            this._stream = new NetworkStream(socket);
            try
            {
                this.Socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveData(), null);
            }
            catch { }

            this.Address = address;
            this.Port = port;
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


        public async Task<string> ReadLineAsync()
        {
            byte[] bs = new byte[1];
            List<byte> lineBuffer = new List<byte>(1024);
            int readed;
            while (true)
            {
                readed = await _stream.ReadAsync(bs, 0, 1);
                if (readed <= 0)
                    throw new SocketException();

                if (bs[0] == 10)
                {
                    break;
                }
                else if (bs[0] != 13)
                {
                    lineBuffer.Add(bs[0]);
                }
            }


            return this.code.GetString(lineBuffer.ToArray());
        }


        public void WriteLine(string text)
        {
            byte[] buffer = this.Encoding.GetBytes($"{text}\r\n");
            this.InnerStream.Write(buffer, 0, buffer.Length);
        }

    }



}
