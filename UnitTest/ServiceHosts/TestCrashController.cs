using JMS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest.ServiceHosts
{
    internal class TestCrashController : MicroServiceControllerBase,IStorageEngine
    {
        public static string FinallyText;
        public static bool CanCrash = true;

        string _text;

        public object CurrentTransaction => _text;

        public void SetText(string text)
        {
            _text = text;
            this.TransactionControl = new JMS.TransactionDelegate(this, this);
        }

        public async Task<string> NoTran(string text)
        {
            return text;
        }

        public void BeginTransaction()
        {
            
        }

        public void CommitTransaction()
        {
            if (CanCrash)
            {
                CanCrash = false;
                throw new Exception("故意宕机");
            }

            FinallyText = _text;
        }

        public void RollbackTransaction()
        {
            _text = null;
        }
    }
}
