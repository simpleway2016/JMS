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
        protected override string GetDesignString()
        {
            var result = new StringBuilder();
            result.Append("\r\n");
            result.Append("H4sIAAAAAAAAE81UTW8aMRD9Lz5DBcsSArdkOWTVKqq6UVWpm8OsPRArXju1vakQ4r937F2Co6QRx5yYjzfP762Y2bM7aBQ6tvq978NbaJGtWPVHSY9sxH6Yv3239Nj20QCRgto/QXWUTA+jl7rfPeGpwwqL4DFyX3EvjWYJlhvtUfsEvq97HTVbUSgF/U4vRnUcoLhm");
            result.Append("FdpnyfG6k0oURuCNdN7YXc0ItAYPDTgs1wTNqSC/Gf5I8eRASWFU12pXBxNH6sss5Q41ykt31XlTam6xJXXU8rbDwAD6tlOKChtQLlREc0d2++GAHA3yo4IovHTfv8Zs4DBWoI2PT2J3LR23spUayMaRmT7Ri8LZO+77/F2pR2UnrcPDidRnsPwBbGRQqLf+IZbnkw8N");
            result.Append("HJlPDqbnOchTByFwT8A/g/7sPP3zVH+hwLlP8v1nH+i/D731r8LojdzGP31fiduQ7qAYlibd55zGw+pXnnY3DFA6rE88AX2cHoKwesFfuB07R7fiS6n9RR4feoUezsNbfOWt1Nu3A6cbcf7MK0//lXYfbIVuhX4YzMRm0eDlZrzIeD7Ol3w2brLpfCwyDrBYLJczmLDD");
            result.Append("P+75Nkw3BQAA");
            return result.ToString();
        }
    }
}

/*<design>
H4sIAAAAAAAAE8WTTY/aMBCG/wqac6BJgLJE6gEStU0/Vkhsq0pND4Njdt01dmQ7dBHiv3ecQHZF2e6thYM/xvP68TuTPWTocIWWQ7IHUUIyCmBh9E/OXJ5BEgVwjRsKwpKbrWB86dDV9qvgv7iBAMrVza6icBwA02rpDJ0sSbFndW0Yf1PA4NWFzEG5KsCnS7lAd0dJ
+0L1egX4/YTGNCno98VyY/3kA7J7P7aqfmZ4pZvQbItSK4GzqpKCoRNaRX/bz+afdcllk3uBLJunWjn+4AbMFhC0VFZsKslbMvqrA6GLT5rddwYtK2TepZM6HXhXezshLteTFb9a9ycxG/VHUzbsr+Jo3C9jhjiZTKdDDOEQAOXVkltIvrd1GHfOb1Ao0jsVypeFipTb
t1qWVIRkjdLyACo0XDVFCw8/ArjB1VO56PV5Jee1kGVKtO+Fddrs/rzi+MRHuVydU0aTUyhr72gP+BU94IEaIwwD2FEsjL1Mnn3LVckfGglaZ/OMS+44mb4Wt91mqmW9UU+uuYo7fFr7189qp3PFDN/QqyFxpiYPUlTXtZSdJ6f2BEFnzkhzu/jo522mNuSlvyr0kUxY
ZsRGKCRnjmqH4MQyPLeyWV2AOlJ0VO1VHdQWDbtD/xlJrm6bz2AcPod51Oo4o5c5Rx2nH2zTov+YMn6Z8rHRU4nW/g8vh89SUjcyT1UZXXHjRNu3h98JhXxVNwUAAA==
<design>*/

