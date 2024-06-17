using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JMS.Common
{
    class BlackListItem
    {
        DateTime _updateTime = DateTime.Now;
        int _errorCount = 0;
        public int ErrorCount => _errorCount;
        public DateTime UpdateTime => _updateTime;

        public void MarkError()
        {
            _updateTime = DateTime.Now;
            Interlocked.Increment(ref _errorCount);
        }
    }
}
