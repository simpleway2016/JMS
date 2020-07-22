using System;
using System.Collections.Generic;
using System.Text;

namespace JMS
{
    /// <summary>
    /// 后台定时运行的任务
    /// </summary>
    public interface IScheduleTask
    {
        /// <summary>
        /// 指定每天在哪个时间点执行
        /// 如new float[] { 1.30 , 22 }，表示每天1:30的时候执行一次
        /// 和22点 的时候执行一次
        /// 如果是null，空数组，表示一直运行
        /// </summary>
        double[] Timers { get; }

        /// <summary>
        /// 运行间隔，单位：毫秒 （在Timers为null时，此间隔有效）
        /// </summary>
        int Interval { get; }
        /// <summary>
        /// 运行任务，此方法会每隔指定的间隔执行一次
        /// </summary>
        void Run();
    }
}
