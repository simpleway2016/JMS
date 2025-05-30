﻿
using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using JMS.Common;
using JMS.Common.IO;
using System.IO.Compression;
using JMS.HttpProxy.Dtos;

namespace JMS.HttpProxy.Applications.Http
{
    /// <summary>
    /// 处理StaticFiles请求
    /// </summary>
    class StaticFilesRequestHandler
    {
        private readonly ILogger<StaticFilesRequestHandler> _logger;
       
        public StaticFilesRequestHandler(ILogger<StaticFilesRequestHandler> logger)
        {
            _logger = logger;
        }
      

        public async Task Handle(NetClient client, Dictionary<string, string> headers,string requestPathLine,ProxyConfig proxyConfig)
        {            

            var requestPathLineArr = requestPathLine.Split(' ');
            var httpMethod = requestPathLineArr[0];
            var requestPath = requestPathLineArr[1];

            if (httpMethod == "OPTIONS")
            {
                headers.Clear();
                client.OutputHttp204(headers);
                return;
            }

            int flag;
            if ((flag = requestPath.IndexOf("?")) > 0)
            {
                requestPath = requestPath.Substring(0, flag);
            }
            if(requestPath == "/")
            {
                if (string.IsNullOrEmpty(proxyConfig.DefaultPage))
                    proxyConfig.DefaultPage = "index.html";

                requestPath = $"/{proxyConfig.DefaultPage}";
            }

            string filepath = Path.Combine(proxyConfig.RootPath, requestPath.Substring(1));

            if (File.Exists(filepath) == false)
            {
                headers.Clear();
                client.OutputHttpNotFund();
                return;
            }

            var lastWriteTime = new FileInfo(filepath).LastWriteTimeUtc.ToString("R");
            headers.TryGetValue("If-Modified-Since", out string since);
            if (lastWriteTime == since)
            {
                client.OutputNotModified();
            }
            else
            {
                await outputFile(client, filepath, lastWriteTime, headers,proxyConfig);
            }
            headers.Clear();
        }

        const int FileBufSize = 20480;
        async Task outputFile(NetClient client, string filePath, string lastModifyTime, Dictionary<string, string> headers, ProxyConfig proxyConfig)
        {
            byte[] bs = null;
            try
            {
                int statusCode = 200;
                bool acceptGzip = false;
                if (headers.TryGetValue("Accept-Encoding", out string acceptEncoding) && acceptEncoding.Contains("gzip"))
                {
                    acceptGzip = true;
                }
                using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite))
                {
                    bs = ArrayPool<byte>.Shared.Rent((int)Math.Min(fs.Length, FileBufSize));
                    Dictionary<string, string> outputHeaders = new Dictionary<string, string>();
                    outputHeaders["Server"] = "JMS";
                    outputHeaders["Date"] = DateTime.UtcNow.ToString("R");
                    outputHeaders["Last-Modified"] = lastModifyTime;
                    outputHeaders["Access-Control-Allow-Origin"] = proxyConfig.AccessControlAllowOrigin??"*";
                    outputHeaders["Content-Type"] = GetContentType(Path.GetExtension(filePath));
                    if (client.KeepAlive)
                    {
                        outputHeaders["Connection"] = "keep-alive";
                    }

                    int range = -1, rangeEnd = 0;
                    if (headers.TryGetValue("Range", out string RangeStr))
                    {
                        #region Range
                        statusCode = 206;
                        var rangeInfo = RangeStr.Replace("bytes=", "").Split('-');
                        if (string.IsNullOrWhiteSpace(rangeInfo[0]))
                        {
                            if (rangeInfo.Length == 1 || string.IsNullOrWhiteSpace(rangeInfo[1]))
                            {
                                outputHeaders.Clear();
                                client.KeepAlive = false;
                                client.OutputHttpCode(416, "Range Not Satisfiable");
                                return;
                            }
                            //Convert.ToInt32(rangeInfo[1])表示读取最后多少个字节
                            rangeEnd = (int)fs.Length - 1;
                            range = rangeEnd - Convert.ToInt32(rangeInfo[1]) + 1;
                            
                        }
                        else
                        {
                            range = Convert.ToInt32(rangeInfo[0]);
                            if (string.IsNullOrWhiteSpace(rangeInfo[1]))
                            {
                                rangeEnd = (int)fs.Length - 1;
                            }
                            else
                            {
                                rangeEnd = Convert.ToInt32(rangeInfo[1]);
                                if(rangeEnd >= fs.Length)
                                    rangeEnd = (int)fs.Length - 1;
                            }
                        }
                        outputHeaders["Accept-Ranges"] = "bytes";
                        outputHeaders["Content-Range"] = $"bytes {range}-{rangeEnd}/{fs.Length}";
                        outputHeaders["Content-Length"] = (rangeEnd - range + 1).ToString();

                        client.WriteLine($"HTTP/1.1 {statusCode} OK");
                        foreach (var pair in outputHeaders)
                        {
                            client.WriteLine($"{pair.Key}: {pair.Value}");
                        }
                        outputHeaders.Clear();

                        client.InnerStream.WriteByte(13);
                        client.InnerStream.WriteByte(10);

                        fs.Seek(range , SeekOrigin.Begin);
                        int totalRead = rangeEnd - range + 1;
                        while (totalRead > 0)
                        {
                            int toread = Math.Min(bs.Length, totalRead);
                            int count = await fs.ReadAsync(bs, 0, toread);
                            if (count == 0)
                                break;
                            totalRead -= count;
                            await client.InnerStream.WriteAsync(bs, 0, count);
                        } 
                        #endregion
                    }
                    else
                    {
                        if(acceptGzip && fs.Length < bs.Length)
                        {
                            acceptGzip = false;
                        }

                        if (acceptGzip)
                        {
                            outputHeaders["Vary"] = "Accept-Encoding";
                            outputHeaders["Content-Encoding"] = "gzip";
                            outputHeaders["Transfer-Encoding"] = "chunked";                            
                        }
                        else
                        {
                            outputHeaders["Content-Length"] = fs.Length.ToString();
                        }
                      

                        client.WriteLine($"HTTP/1.1 {statusCode} OK");
                        foreach (var pair in outputHeaders)
                        {
                            client.WriteLine($"{pair.Key}: {pair.Value}");
                        }
                        outputHeaders.Clear();

                        client.InnerStream.WriteByte(13);
                        client.InnerStream.WriteByte(10);


                        var totalRead = fs.Length;
                        if (acceptGzip)
                        {
                            using var stream = new WriteCallbackStream();
                            stream.Callback = (data, offset, count) =>
                            {
                                client.InnerStream.Write(Encoding.UTF8.GetBytes($"{count.ToString("x")}\r\n"));
                                client.InnerStream.Write(data, offset, count);
                                client.InnerStream.WriteByte(13);
                                client.InnerStream.WriteByte(10);
                            };

                            using (var zipStream = new GZipStream(stream, CompressionMode.Compress))
                            {                               
                                while (totalRead > 0)
                                {
                                    int toread = Math.Min(bs.Length, (int)totalRead);
                                    int count = await fs.ReadAsync(bs, 0, toread);
                                    if (count == 0)
                                        break;
                                    totalRead -= count;
                                    zipStream.Write(bs, 0, count);
                                }
                              
                                zipStream.Close();
                            }

                          

                            client.InnerStream.Write(Encoding.UTF8.GetBytes("0\r\n\r\n"));
                        }
                        else
                        {
                         
                            while (totalRead > 0)
                            {
                                int toread = Math.Min(bs.Length, (int)totalRead);
                                int count = await fs.ReadAsync(bs, 0, toread);
                                if (count == 0)
                                    break;
                                totalRead -= count;

                                await client.InnerStream.WriteAsync(bs, 0, count);
                            }
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {

                throw;
            }
            finally
            {
                if(bs != null)
                {
                    ArrayPool<byte>.Shared.Return(bs);
                }
               
            }

        }

        static string GetContentType(string fileExtension)
        {
            if (HttpProxyProgram.Config.Current.ContentTypes != null && HttpProxyProgram.Config.Current.ContentTypes.TryGetValue(fileExtension, out string v))
                return v;

            // 如果未找到，返回一个默认的 Content-Type
            return "application/octet-stream";
        }

    }
}
