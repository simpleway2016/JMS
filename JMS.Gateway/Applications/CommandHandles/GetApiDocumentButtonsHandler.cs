using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using System.Threading.Tasks;
using JMS.ApiDocument;

namespace JMS.Applications.CommandHandles
{
    class GetApiDocumentButtonsHandler : ICommandHandler
    {
        IDocumentButtonProvider _documentButtonProvider;

        public GetApiDocumentButtonsHandler(IDocumentButtonProvider documentButtonProvider)
        {
            this._documentButtonProvider = documentButtonProvider;

        }
        public CommandType MatchCommandType => CommandType.GetApiDocumentButtons;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            try
            {
                if (cmd.Content != null)
                {
                    //返回指定button
                    netclient.WriteServiceData(new InvokeResult
                    {
                        Success = true,
                        Data = _documentButtonProvider.ApiDocCodeBuilders.Where(m=>m.Name == cmd.Content || m.Name == "vue methods").ToArray()
                    });
                }
                else
                {
                    netclient.WriteServiceData(new InvokeResult
                    {
                        Success = true,
                        Data = _documentButtonProvider.ApiDocCodeBuilders.ToArray()
                    });
                }
            }
            catch (Exception ex)
            {
                netclient.WriteServiceData(new InvokeResult
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
}
