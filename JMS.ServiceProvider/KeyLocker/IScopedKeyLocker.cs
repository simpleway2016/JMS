using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    /// <summary>
    /// 依赖注入作用域内的分布式锁，离开作用域自动释放锁
    /// </summary>
    public interface IScopedKeyLocker
    {
        /// <summary>
        /// 申请锁住指定的key
        /// </summary>
       /// <param name="key"></param>
        /// <returns>是否成功</returns>
        bool TryLock(string key);

        /// <summary>
        /// 申请锁住指定的key
        /// </summary>
        /// <param name="key"></param>
        /// <returns>是否成功</returns>
        Task<bool> TryLockAsync(string key);

        bool TryUnLock(string key);

        Task<bool> TryUnLockAsync(string key);

        /// <summary>
        /// 强制释放锁定的key（慎用）
        /// </summary>
        /// <param name="key"></param>
        void UnLockAnyway(string key);


        /// <summary>
        /// 强制释放锁定的key（慎用）
        /// </summary>
        /// <param name="key"></param>
        Task UnLockAnywayAsync(string key);

        string[] GetLockedKeys();

        void RemoveKeyFromLocal(string key);
        /// <summary>
        /// 释放本服务所有key
        /// </summary>
        void UnLockAllKeys();
    }

    class DefaultScopedKeyLocker : IScopedKeyLocker,IDisposable
    {
        readonly IKeyLocker _keyLocker;
        ConcurrentDictionary<string, bool> _lockedKeys = new ConcurrentDictionary<string, bool>();

        string _transactionid;
        public string TransactionId
        {
            get
            {
                if (_transactionid == null)
                {
                    if (BaseJmsController.RequestingObject.Value != null)
                    {
                        _transactionid = BaseJmsController.RequestingObject.Value.Headers["TranId"];
                    }
                    else
                    {
                        _transactionid = Guid.NewGuid().ToString("N");
                    }
                }
                return _transactionid;
            }
        }

        public DefaultScopedKeyLocker(IKeyLocker keyLocker)
        {
            this._keyLocker = keyLocker;
        }

        
        public void Dispose()
        {
            if(_lockedKeys.Count > 0)
            {
                foreach( var pair in _lockedKeys)
                {
                    _keyLocker.UnLockAnyway(pair.Key);
                }
                _lockedKeys.Clear();
            }
        }

        public string[] GetLockedKeys()
        {
            return _keyLocker.GetLockedKeys();
        }

        public void RemoveKeyFromLocal(string key)
        {
            _keyLocker.RemoveKeyFromLocal(key);
        }

        public bool TryLock(string key)
        {
            if( _keyLocker.TryLock(this.TransactionId, key))
            {
                _lockedKeys.TryAdd(key, true);
                return true;
            }

            return false;
        }

        public async Task<bool> TryLockAsync(string key)
        {
            if(await _keyLocker.TryLockAsync(this.TransactionId, key))
            {
                _lockedKeys.TryAdd(key, true);
                return true;
            }

            return false;
        }

        public bool TryUnLock( string key)
        {
            if( _keyLocker.TryUnLock(this.TransactionId, key))
            {
                _lockedKeys.TryRemove(key, out bool o);
                return true;
            }
            return false;
        }

        public async Task<bool> TryUnLockAsync(string key)
        {
            if (await _keyLocker.TryUnLockAsync(this.TransactionId, key))
            {
                _lockedKeys.TryRemove(key, out bool o);
                return true;
            }
            return false;
        }

        public void UnLockAllKeys()
        {
            _keyLocker.UnLockAllKeys();
            _lockedKeys.Clear();
        }

        public void UnLockAnyway(string key)
        {
            _keyLocker.UnLockAnyway(key);
            _lockedKeys.TryRemove(key, out bool o);
        }

        public async Task UnLockAnywayAsync(string key)
        {
            await _keyLocker.UnLockAnywayAsync(key);
            _lockedKeys.TryRemove(key, out bool o);
        }
    }
}
