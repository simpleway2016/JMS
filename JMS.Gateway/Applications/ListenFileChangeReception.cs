using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Engines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace JMS.Applications
{
    class ListenFileChangeReception
    {
        AutoResetEvent _waitObj = new AutoResetEvent(false);
        List<string> _changedFiles = new List<string>();
        string[] _listeningFiles;
        IConfiguration _configuration;
        ILogger<ListenFileChangeReception> _logger;
        string _root;
        public ListenFileChangeReception(IConfiguration configuration, ILogger<ListenFileChangeReception> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _root = configuration.GetValue<string>("ShareFolder");

        }

        private void SystemEventCenter_ShareFileChanged(object sender, string file)
        {

            if (_listeningFiles != null && _listeningFiles.Contains(file))
            {
                lock (_changedFiles)
                {
                    _changedFiles.Add(file);
                }
                _waitObj.Set();
            }
        }

        public async Task Handle(NetClient client, GatewayCommand cmd)
        {
            try
            {
                _listeningFiles = cmd.Content.FromJson<string[]>();
                SystemEventCenter.ShareFileChanged += SystemEventCenter_ShareFileChanged;
                _logger.LogInformation($"远程服务连接监控文件变化，{client.RemoteEndPoint} {_listeningFiles.ToJsonString()}");

                while (true)
                {
                    if (_changedFiles.Count == 0)
                    {
                        client.WriteServiceData(new InvokeResult
                        {
                            Success = true
                        });
                        await client.ReadServiceObjectAsync<InvokeResult>();
                    }
                    else
                    {
                        string[] sendFiles = null;
                        lock (_changedFiles)
                        {
                            sendFiles = _changedFiles.ToArray();
                            _changedFiles.Clear();
                        }

                        foreach (var file in sendFiles)
                        {
                            string fullpath = $"{_root}/{file}";
                            if (File.Exists(fullpath))
                            {
                                byte[] data = null;
                                try
                                {
                                    data = await File.ReadAllBytesAsync(fullpath);

                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, ex.Message);
                                    continue;
                                }

                                client.WriteServiceData(new InvokeResult
                                {
                                    Success = true,
                                    Data = file
                                });

                                client.Write(data.Length);
                                client.Write(data);
                                await client.ReadServiceObjectAsync<InvokeResult>();
                            }
                        }

                    }


                    _waitObj.WaitOne(38000);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                _logger.LogInformation($"断开监控文件变化，{client.RemoteEndPoint}");
                SystemEventCenter.ShareFileChanged -= SystemEventCenter_ShareFileChanged;
            }

        }
    }
}
