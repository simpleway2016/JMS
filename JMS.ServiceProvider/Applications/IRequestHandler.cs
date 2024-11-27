using JMS.Dtos;
using System.Threading.Tasks;

namespace JMS.Applications
{
    interface IRequestHandler
    {
        InvokeType MatchType { get; }
        Task Handle(NetClient netclient, InvokeCommand cmd);
    }
}
