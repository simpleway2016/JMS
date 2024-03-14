using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MoneyAccountServiceHost.Controllers
{
    public class MoneyAccountController : BaseController
    {
        /// <summary>
        /// 给用户开户
        /// </summary>
        /// <param name="userid">用户id</param>
        /// <returns></returns>
        public async Task<long> CreateAccount(long userid)
        {
            //支持分布式事务，必须先启动事务
            this.CurrentDBContext.BeginTransaction();

            using var cmd = this.CurrentDBContext.Connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)this.CurrentDBContext.CurrentTransaction;
            cmd.CommandText = @$"
INSERT INTO account (userid, balance)  
VALUES ( {userid}, 0);
SELECT last_insert_rowid();
";
            return (long)await cmd.ExecuteScalarAsync();
        }

        /// <summary>
        /// 给用户增加余额
        /// </summary>
        /// <param name="accountId">资金账户id</param>
        /// <param name="amout"></param>
        /// <returns>余额</returns>
        public async Task<decimal> AddMoney(long accountId, decimal amout)
        {
            //支持分布式事务，必须先启动事务
            this.CurrentDBContext.BeginTransaction();

            using var cmd = this.CurrentDBContext.Connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)this.CurrentDBContext.CurrentTransaction;
            cmd.CommandText = @$"
update account set balance=balance+{amout} where id={accountId};
select balance from account where id={accountId};
";
            return  Convert.ToDecimal( await cmd.ExecuteScalarAsync() );
        }


    }
}
