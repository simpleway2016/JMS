using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Dtos
{
    public class LockKeyInfo
    {
        public string MicroServiceId;
        public string Key;
        /// <summary>
        /// 一直等待，直到成功
        /// </summary>
        public bool WaitToSuccess;
        public bool IsUnlock;
    }
}
