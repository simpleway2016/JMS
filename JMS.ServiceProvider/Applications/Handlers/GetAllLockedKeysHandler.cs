using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
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
