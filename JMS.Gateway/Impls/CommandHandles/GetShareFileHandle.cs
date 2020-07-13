using JMS.Interfaces;
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

namespace JMS.Impls.CommandHandles
{
    class GetShareFileHandle : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _gateway;
        IConfiguration _configuration;
        public GetShareFileHandle(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _gateway = _serviceProvider.GetService<Gateway>();
            _configuration = _serviceProvider.GetService<IConfiguration>();
        }
        public CommandType MatchCommandType => CommandType.GetShareFile;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var filepath = cmd.Content;
            var root = _configuration.GetValue<string>("ShareFolder");

            byte[] data = File.ReadAllBytes($"{root}/{filepath}");

            netclient.WriteServiceData(new InvokeResult
            {
                Success = true,
                Data = data.Length
            });
            netclient.Write(data);

        }
    }
}
