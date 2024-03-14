using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UserInfoServiceHost.Controllers
{
    public class UserInfoController : BaseController
    {
        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>用户id</returns>
        public async Task<long> Register(string username, string password)
        {
            //支持分布式事务，必须先启动事务
            this.CurrentDBContext.BeginTransaction();

            using var cmd = this.CurrentDBContext.Connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)this.CurrentDBContext.CurrentTransaction;
            cmd.CommandText = @$"
INSERT INTO userinfo (username, password)  
VALUES ( '{username}', '{password}');
SELECT last_insert_rowid();
";
            var id = (long)await cmd.ExecuteScalarAsync();
            return id;
        }

        /// <summary>
        /// 设置用户密码
        /// </summary>
        /// <param name="userid">用户id</param>
        /// <param name="password">新密码</param>
        /// <returns></returns>
        public async Task SetUserPassword(long userid, string password)
        {
            //支持分布式事务，必须先启动事务
            this.CurrentDBContext.BeginTransaction();

            using var cmd = this.CurrentDBContext.Connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)this.CurrentDBContext.CurrentTransaction;
            cmd.CommandText = @$"
update userinfo set password='{password}' where id={userid}
";
            await cmd.ExecuteNonQueryAsync();
        }


        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public async Task<string> GetUserInfo(long userid)
        {
            using var cmd = this.CurrentDBContext.Connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)this.CurrentDBContext.CurrentTransaction;
            cmd.CommandText = @$"
select * from userinfo where id={userid}
";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    id = reader["id"],
                    username = reader["username"],
                    password = reader["password"]
                });
            }
            return null;
        }
    }
}
