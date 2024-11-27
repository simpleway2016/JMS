using JMS.Dtos;
using System.Threading.Tasks;

namespace JMS.Applications
{
    class GetAllLockedKeysHandler : IRequestHandler
    {

        IKeyLocker  _keyLocker;
        public GetAllLockedKeysHandler(IKeyLocker keyLocker)
        {
            _keyLocker = keyLocker;
        }

        public InvokeType MatchType => InvokeType.GetAllLockedKeys;

        public async Task Handle(NetClient netclient, InvokeCommand cmd)
        {            
            netclient.WriteServiceData(new InvokeResult { 
                Success = true,
                Data = _keyLocker.GetLockedKeys()
            });
        }
    }
}
