using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using JMS.Dtos;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using System.Threading;
using System.IO;
using Microsoft.Extensions.Logging;

namespace JMS.Applications.CommandHandles
{
    class GetShareFileHandle : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _gateway;
        IConfiguration _configuration;
        ILogger<GetShareFileHandle> _logger;
        public GetShareFileHandle(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _gateway = _serviceProvider.GetService<Gateway>();
            _configuration = _serviceProvider.GetService<IConfiguration>();
            _logger = _serviceProvider.GetService<ILogger<GetShareFileHandle>>();
        }
        public CommandType MatchCommandType => CommandType.GetShareFile;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var filepath = cmd.Content;
            var root = _configuration.GetValue<string>("ShareFolder");

            filepath = $"{root}/{filepath}";
            _logger?.LogDebug("getting file:{0}" , filepath);
            if (File.Exists(filepath))
            {
                byte[] data = File.ReadAllBytes(filepath);

                netclient.WriteServiceData(new InvokeResult
                {
                    Success = true,
                    Data = data.Length
                });
                netclient.Write(data);
            }
            else
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false,
                    Error = "file not found"
                });
            }

        }
    }
}
