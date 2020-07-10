using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Interfaces.Hardware
{
    interface ICpuInfo
    {
        /// <summary>
        /// 获取当前计算机cpu利用率
        /// </summary>
        /// <returns></returns>
        double GetCpuUsage();
    }
}
