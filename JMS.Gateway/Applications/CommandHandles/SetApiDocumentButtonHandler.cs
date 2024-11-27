using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using JMS.Dtos;
using System.Threading.Tasks;
using JMS.ApiDocument;
using JMS.Authentication;

namespace JMS.Applications.CommandHandles
{
    class SetApiDocumentButtonHandler : ICommandHandler
    {
        IAuthentication _authentication;
        IDocumentButtonProvider _documentButtonProvider;

        public SetApiDocumentButtonHandler(IDocumentButtonProvider documentButtonProvider, IAuthentication authentication)
        {
            this._authentication = authentication;
            this._documentButtonProvider = documentButtonProvider;

        }
        public CommandType MatchCommandType => CommandType.SetApiDocumentButton;

        public async Task Handle(NetClient netclient, GatewayCommand cmd)
        {
            try
            {
                if (!(await _authentication.Verify(netclient, cmd)))
                {
                    return;
                }
                var data = cmd.Content.FromJson<ApiDocCodeBuilder>();
                var item = _documentButtonProvider.ApiDocCodeBuilders.FirstOrDefault(m=>m.Name == data.Name);
                if(item == null)
                {
                    item = new ApiDocCodeBuilder { 
                        Name = data.Name,
                    };
                }
                if (!string.IsNullOrEmpty(data.Code))
                {
                    item.Code = data.Code;
                }
                _documentButtonProvider.ApiDocCodeBuilders.AddOrUpdate(item);

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
