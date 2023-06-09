using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using System.Threading.Tasks;
using JMS.Domains.ApiDocument;

namespace JMS.Applications.CommandHandles
{
    class RemoveApiDocumentButtonHandler : ICommandHandler
    {
        IAuthentication _authentication;
        IDocumentButtonProvider _documentButtonProvider;

        public RemoveApiDocumentButtonHandler(IDocumentButtonProvider documentButtonProvider, IAuthentication authentication)
        {
            this._authentication = authentication;
            this._documentButtonProvider = documentButtonProvider;

        }
        public CommandType MatchCommandType => CommandType.RemoveApiDocumentButton;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            try
            {
                if (!(await _authentication.Verify(netclient, cmd)))
                {
                    return;
                }

                var buttonName = cmd.Content;
                var item = _documentButtonProvider.ApiDocCodeBuilders.FirstOrDefault(m => m.Name == buttonName);
                if (item != null)
                {
                    _documentButtonProvider.ApiDocCodeBuilders.Remove(item);
                }

                netclient.WriteServiceData(new InvokeResult
                {
                    Success = true
                });
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
