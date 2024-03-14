//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Storage;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading;

//namespace UserInfoServiceHost
//{
//    /// <summary>
//    /// 请把父类改为你相应db context类
//    /// 请引用相关的Microsoft.EntityFrameworkCore包
//    /// </summary>
//    public class SystemDBContext : DbContext, JMS.IStorageEngine
//{
//    /// <summary>
//    /// 当前事务对象
//    /// </summary>
//    public object CurrentTransaction => this.Database.CurrentTransaction;

//    public void BeginTransaction()
//    {
//        if (this.CurrentTransaction == null)
//        {
//            this.Database.BeginTransaction();
//        }
//    }
//    public void CommitTransaction()
//    {
//        if (this.CurrentTransaction != null)
//        {
//            this.Database.CommitTransaction();
//        }
//    }
//    public void RollbackTransaction()
//    {
//        if (this.CurrentTransaction != null)
//        {
//            this.Database.RollbackTransaction();
//        }
//    }
//}
//}
