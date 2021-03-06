﻿using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using JMS.Dtos;

namespace JMS.Impls.CommandHandles
{
    class GetAllServiceProvidersHandler : ICommandHandler
    {
        IServiceProvider _serviceProvider;
        Gateway _Gateway;
        public GetAllServiceProvidersHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _Gateway = serviceProvider.GetService<Gateway>();
        }
        public CommandType MatchCommandType => CommandType.GetAllServiceProviders;

        public void Handle(NetClient netclient, GatewayCommand cmd)
        {
            var locations = this.List(cmd.Content);
            netclient.WriteServiceData(locations);

        }

        public RegisterServiceRunningInfo[] List(string serviceName)
        {
            var list = _Gateway.GetAllServiceProviders().AsQueryable();
            if (!string.IsNullOrEmpty(serviceName))
            {
                list = list.Where(m => m.ServiceNames.Contains(serviceName));
            }

            var allocator = _serviceProvider.GetService<IServiceProviderAllocator>();
            return list.Select(m => new RegisterServiceRunningInfo
            {
                Host = m.Host,
                ServiceAddress = m.ServiceAddress,
                Port = m.Port,
                ServiceId = m.ServiceId,
                ServiceNames = m.ServiceNames,
                Description = m.Description,
                MaxThread = m.MaxThread,
                PerformanceInfo = allocator.GetPerformanceInfo(m)
            }).ToArray();
        }
    }
}
