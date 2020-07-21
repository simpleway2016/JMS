using JMS.Dtos;
using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class GetAllLockedKeysHandler : IRequestHandler
    {

        IKeyLocker  _keyLocker;
        public GetAllLockedKeysHandler(IKeyLocker keyLocker)
        {
            _keyLocker = keyLocker;
        }

        public InvokeType MatchType => InvokeType.GetAllLockedKeys;

        public void Handle(NetClient netclient, InvokeCommand cmd)
        {            
            netclient.WriteServiceData(new InvokeResult { 
                Success = true,
                Data = _keyLocker.GetLockedKeys()
            });
        }
    }
}
