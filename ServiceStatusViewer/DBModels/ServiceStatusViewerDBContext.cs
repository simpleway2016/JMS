using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Way.EntityDB.Attributes;
using System.Diagnostics.CodeAnalysis;

namespace DBModels
{
    [TableConfig]
    [Table("servicebuildcodehistory")]
    [Way.EntityDB.DataItemJsonConverter]
    public class ServiceBuildCodeHistory :Way.EntityDB.DataItem
    {
        System.Nullable<Int32> _id;
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [DisallowNull]
        [Column("id")]
        public virtual System.Nullable<Int32> id
        {
            get
            {
                return _id;
            }
            set
            {
                if ((_id != value))
                {
                    SendPropertyChanging("id",_id,value);
                    _id = value;
                    SendPropertyChanged("id");
                }
            }
        }
        String _ServiceName;
        [MaxLength(50)]
        [Column("servicename")]
        public virtual String ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                if ((_ServiceName != value))
                {
                    SendPropertyChanging("ServiceName",_ServiceName,value);
                    _ServiceName = value;
                    SendPropertyChanged("ServiceName");
                }
            }
        }
        String _Namespace;
        [MaxLength(50)]
        [Column("namespace")]
        public virtual String Namespace
        {
            get
            {
                return _Namespace;
            }
            set
            {
                if ((_Namespace != value))
                {
                    SendPropertyChanging("Namespace",_Namespace,value);
                    _Namespace = value;
                    SendPropertyChanged("Namespace");
                }
            }
        }
        String _ClassName;
        [MaxLength(50)]
        [Column("classname")]
        public virtual String ClassName
        {
            get
            {
                return _ClassName;
            }
            set
            {
                if ((_ClassName != value))
                {
                    SendPropertyChanging("ClassName",_ClassName,value);
                    _ClassName = value;
                    SendPropertyChanged("ClassName");
                }
            }
        }
    }
    /// <summary>
    /// 方法调用历史
    /// </summary>
    [TableConfig]
    [Table("invokehistory")]
    [Way.EntityDB.DataItemJsonConverter]
    public class InvokeHistory :Way.EntityDB.DataItem
    {
        System.Nullable<Int64> _id;
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [DisallowNull]
        [Column("id")]
        public virtual System.Nullable<Int64> id
        {
            get
            {
                return _id;
            }
            set
            {
                if ((_id != value))
                {
                    SendPropertyChanging("id",_id,value);
                    _id = value;
                    SendPropertyChanged("id");
                }
            }
        }
        String _ServiceName;
        [MaxLength(50)]
        [Column("servicename")]
        public virtual String ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                if ((_ServiceName != value))
                {
                    SendPropertyChanging("ServiceName",_ServiceName,value);
                    _ServiceName = value;
                    SendPropertyChanged("ServiceName");
                }
            }
        }
        String _MethodName;
        [MaxLength(50)]
        [Column("methodname")]
        public virtual String MethodName
        {
            get
            {
                return _MethodName;
            }
            set
            {
                if ((_MethodName != value))
                {
                    SendPropertyChanging("MethodName",_MethodName,value);
                    _MethodName = value;
                    SendPropertyChanged("MethodName");
                }
            }
        }
        Byte[] _Header;
        [Column("header")]
        public virtual Byte[] Header
        {
            get
            {
                return _Header;
            }
            set
            {
                if ((_Header != value))
                {
                    SendPropertyChanging("Header",_Header,value);
                    _Header = value;
                    SendPropertyChanged("Header");
                }
            }
        }
        Byte[] _Parameters;
        [Column("parameters")]
        public virtual Byte[] Parameters
        {
            get
            {
                return _Parameters;
            }
            set
            {
                if ((_Parameters != value))
                {
                    SendPropertyChanging("Parameters",_Parameters,value);
                    _Parameters = value;
                    SendPropertyChanged("Parameters");
                }
            }
        }
    }
}

namespace DBModels.DB
{
    public class ServiceStatusViewer : Way.EntityDB.DBContext
    {
         public ServiceStatusViewer(string connection, Way.EntityDB.DatabaseType dbType , bool upgradeDatabase = true): base(connection, dbType , upgradeDatabase)
        {
            if (!setEvented)
            {
                lock (lockObj)
                {
                    if (!setEvented)
                    {
                        setEvented = true;
                        Way.EntityDB.DBContext.BeforeDelete += Database_BeforeDelete;
                    }
                }
            }
        }
        static object lockObj = new object();
        static bool setEvented = false;
        static void Database_BeforeDelete(object sender, Way.EntityDB.DatabaseModifyEventArg e)
        {
             var db =  sender as DBModels.DB.ServiceStatusViewer;
            if (db == null) return;
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ServiceBuildCodeHistory>().HasKey(m => m.id);
            modelBuilder.Entity<InvokeHistory>().HasKey(m => m.id);
        }
        System.Linq.IQueryable<ServiceBuildCodeHistory> _ServiceBuildCodeHistory;
        public virtual System.Linq.IQueryable<ServiceBuildCodeHistory> ServiceBuildCodeHistory
        {
            get
            {
                if (_ServiceBuildCodeHistory == null)
                {
                    _ServiceBuildCodeHistory = this.Set<ServiceBuildCodeHistory>();
                }
                return _ServiceBuildCodeHistory;
            }
        }
        System.Linq.IQueryable<InvokeHistory> _InvokeHistory;
        public virtual System.Linq.IQueryable<InvokeHistory> InvokeHistory
        {
            get
            {
                if (_InvokeHistory == null)
                {
                    _InvokeHistory = this.Set<InvokeHistory>();
                }
                return _InvokeHistory;
            }
        }
        protected override string GetDesignString()
        {
            var result = new StringBuilder();
            result.Append("\r\n");
            result.Append("H4sIAAAAAAAACtVXXW/aMBT9L36GCUIg0LcOJjXaxqpRVZNKH4x9AauOw2IHhKr+9107aUhXmvLRdfSJ++Xrew/Hh/aeXNGJBE3Obu4zc0gjIGdk9FsKA6RGfsarLBsaiDIrLxEc09dUpug0H2pF3KwXsMmQfgLUgOt9zoyIFSnVslgZUKZUfj/O5hiTMzQFx89mpzZ2");
            result.Append("B9AekxEkS8Hgcyok78ccLoQ2cbIeEywaUEMnVEM4wFIfA+JbzO7Qbjyg049lGimN7k3RuuuVe9sY+qE+T00cKpZAhNNhyiQp2A5UDVMpMTClUtsIn1zhutlhW1nLx3cTuMFDffnVeXmPOOGQuMsbLjsQmiUiEoriGo+dEaJiwtaW7TN/66iPk21mzS8ujbqkCZtTvA3D");
            result.Append("EtTMzF243ahc4LHzZoPmbhv45Q2soReUncL83m7zt8vz9yXV+kTwb1XMf2tzg1/9WE3FzJE+i7jXUH6DPH805ffs43H79EcG3657Pq8IgPcvBSCwPGN0YQ+jqxDW8jcSqmV8d5wOBIFfecVRwjARs0Ibslje/otKowFMhSrFCjrkPocpTaVx6GyiG4o4bA7SmNoTLrvO");
            result.Append("70yaVgVp5lTN9iLND8mLH7AXaDGE1WslClZb6dGupMfbq/IeRCl0Yz+q7KXm27hSCGQQdCrR+Q5mHvMPDo53MDhBJTgXQPGKI4AREZ3B/rC8CSitg0HpVoJySRM0DST6gwLj7y+7zCkeL6mPjXKQiMPf0feTaP/UJfr/QYdu3ptY8DK7DKH9K8RSz/47tdaI8qdQmY7v");
            result.Append("LnpSnQP7vH5kEqFmzw9s0N39zJOdXhzt1q5lsyMw+UGPT4MJdKf1wGN+3e+xVn3iNdt17jFKg6DXa9EGefgDuURHOEoOAAA=");
            return result.ToString();
        }
    }
}

/*<design>
H4sIAAAAAAAACtWWT2/TMBTAv8rkczqStF26SBxoAyzApkodExLl4DpOZ+bYke2MTVNPSBzhAnwGjghOfKANPgbPzRLKWES77bC1B/95z++933t+rU9QhA2eYE1ReIJYgsKOg4ZKvqbExBEKPQft4AyEaETVISN0ZLAp9B6jb6hCDkomu8c5iH0HESlGRoFmAhbXtCwU
offHKAnvXXJ0PZmMkT3P+RCbfTh1MlZjsbYGB0ASwjgIx/B5rqnSdvIEkwM7lobtTNFclqLtkR0ucWO3o/62TCjXDSpRfyCFoUdmnegxcqooNMtyTstI4Au7MwiXPZPkYJ4jm5VRjolNTeUBFB4XNofIT9JgQntpK/BJp9XZJO3WxPe6rcQnGAfB5mYbu2jmIDhXcKpR
+LJMfrdOd4aZAHtVdWwtwGusH0meQObDFHNNHZRjRcW8Uu7slYN28WTRnLcBZcG5YVKgUBScX6xmv2A8GUDwW0wbqY7/9XhO7M6c0mTgLZhEZ59/nH3/9Ovr258fv5y+f3f64RuqXcTiUB4sYbgKOxYXs+EFlSgqWUoFu4JEHcGtc10HHYPMhfuXcjytKO3cLxd14P6C
MUtx0VjX9UpjvY1mYxBsHL2IRUKP5oHCOupHlFND4RqlbFpvDiQvMrEA0/ObigFiW9oHhZGxIIpmUFIUGlVAgQdY7IBuXfCq4RADHSCaL0prD0WRRTRlot7hVExtc5WrhKa44GYP86LW+Cu9sR4+tfPSs1Rw0WzkkONYR0wTxTImMJSzjmbAsdYlxWKuvV77PxdvvroE
urJbUZeh1NCHWJF9rJYGR10XrQB+7r0m965A3mkit4Oe/2Lcem7/CtzdJu4/J249d3tl7iDo3GhXT9j0hhvb/tTdWGMHQffONPYi+PUbOwga/0u3qdmXyd0AX72zgyBoAt+i2L5FVodmGZ7S5ZGvA3yVlu41AQ+xgomBF+nthp6/FJeDhrcKsVu5kjlVhpWvmtlvIAS4
UBgMAAA=
<design>*/

