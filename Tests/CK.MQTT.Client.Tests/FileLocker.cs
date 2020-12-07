using System;
using System.Collections.Generic;
using System.Threading;

namespace CK.MQTT.Client.Tests
{
    class FileLocker
    {
        readonly Dictionary<string, (SemaphoreSlim semaphore, int lockCount)> _lockPool = new Dictionary<string, (SemaphoreSlim, int)>();

        public IDisposable LockFile( string path )
        {
            lock( _lockPool )
            {
                if( _lockPool.ContainsKey( path ) )
                {
                    _lockPool[path] = (_lockPool[path].semaphore, _lockPool[path].lockCount + 1);
                }
                else
                {
                    _lockPool[path] = (new SemaphoreSlim( 1 ), 0);
                }
                _lockPool[path].semaphore.Wait();
            }
            return new Disposable( () => Release( path ) );
        }

        void Release( string path )
        {
            lock( _lockPool )
            {
                if( _lockPool[path].lockCount == 0 )
                {
                    _lockPool.Remove( path );
                }
                else
                {
                    _lockPool[path] = (_lockPool[path].semaphore, _lockPool[path].lockCount - 1);
                }
            }
        }

        struct Disposable : IDisposable
        {
            readonly Action _dispose;

            public Disposable( Action dispose )
            {
                _dispose = dispose;
            }

            public void Dispose() => _dispose();
        }
    }
}
