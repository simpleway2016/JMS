﻿using System;
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
using System.Threading.Tasks;

namespace JMS.Applications.CommandHandles
{
    class GetShareFileHandle : ICommandHandler
    {
        IConfiguration _configuration;
        ILogger<GetShareFileHandle> _logger;
        public GetShareFileHandle(IConfiguration configuration, ILogger<GetShareFileHandle> logger)
        {
            this._configuration = configuration;
            this._logger = logger;
        }
        public CommandType MatchCommandType => CommandType.GetShareFile;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            var filepath = cmd.Content;
            var root = _configuration.GetValue<string>("ShareFolder");

            filepath = $"{root}/{filepath}";
            _logger?.LogDebug("getting file:{0}" , filepath);
            if (File.Exists(filepath))
            {
                byte[] data = await File.ReadAllBytesAsync(filepath);

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
