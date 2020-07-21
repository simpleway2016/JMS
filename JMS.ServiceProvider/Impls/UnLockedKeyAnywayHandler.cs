using JMS.Dtos;
using JMS.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;

namespace JMS.Impls
{
    class UnLockedKeyAnywayHandler : IRequestHandler
    {

        IKeyLocker  _keyLocker;
        public UnLockedKeyAnywayHandler(IKeyLocker keyLocker)
        {
            _keyLocker = keyLocker;
        }

        public InvokeType MatchType => InvokeType.UnlockKeyAnyway;

        public void Handle(NetClient netclient, InvokeCommand cmd)
        {
            _keyLocker.UnLockAnyway(cmd.Method);
            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
