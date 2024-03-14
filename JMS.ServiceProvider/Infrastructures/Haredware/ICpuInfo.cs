using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.Infrastructures.Hardware
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
