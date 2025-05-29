using JMS.ServiceProvider.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JMS
{
    class DefaultAspNetScopedKeyLocker : IScopedKeyLocker,IDisposable
    {
        readonly IKeyLocker _keyLocker;
        private readonly ApiTransactionDelegate _apiTransactionDelegate;
        ConcurrentDictionary<string, bool> _lockedKeys = new ConcurrentDictionary<string, bool>();

        string _transactionid;
        public string TransactionId
        {
            get
            {
                if (_transactionid == null)
                {
                    if (_apiTransactionDelegate.TransactionId != null)
                    {
                        _transactionid = _apiTransactionDelegate.TransactionId;
                    }
                    else
                    {
                        _transactionid = Guid.NewGuid().ToString("N");
                    }
                }
                return _transactionid;
            }
        }

        public DefaultAspNetScopedKeyLocker(IKeyLocker keyLocker, ServiceProvider.AspNetCore.ApiTransactionDelegate apiTransactionDelegate)
        {
            this._keyLocker = keyLocker;
            _apiTransactionDelegate = apiTransactionDelegate;
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
            if(await _keyLocker.TryLockAsync(this.TransactionId, key).ConfigureAwait(false))
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
            if (await _keyLocker.TryUnLockAsync(this.TransactionId, key).ConfigureAwait(false))
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
            await _keyLocker.UnLockAnywayAsync(key).ConfigureAwait(false);
            _lockedKeys.TryRemove(key, out bool o);
        }
    }
}
