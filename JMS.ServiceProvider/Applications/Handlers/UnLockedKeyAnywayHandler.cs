using JMS.Dtos;
using JMS.Domains;
using System;
using System.Collections.Generic;
using System.Text;
using Way.Lib;
using System.Threading.Tasks;

namespace JMS.Applications
{
    class UnLockedKeyAnywayHandler : IRequestHandler
    {

        IKeyLocker  _keyLocker;
        public UnLockedKeyAnywayHandler(IKeyLocker keyLocker)
        {
            _keyLocker = keyLocker;
        }

        public InvokeType MatchType => InvokeType.UnlockKeyAnyway;

        public async Task Handle(NetClient netclient, InvokeCommand cmd)
        {
            await _keyLocker.UnLockAnywayAsync(cmd.Method);
            netclient.WriteServiceData(new InvokeResult { Success = true});
        }
    }
}
