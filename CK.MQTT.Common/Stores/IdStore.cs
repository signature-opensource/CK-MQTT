using CK.Core;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
    class IdStore
    {
        struct Entry
        {
            public int NextFreeId;
            public TaskCompletionSource<object?>? TaskCS;
        }

        Entry[] _entries;
        int _nextFreeId = 1;
        int _count = 0;
        readonly int _maxPacketId;

        public IdStore( int packetIdMaxValue )
        {
            _entries = new Entry[64];
            _maxPacketId = packetIdMaxValue;
        }

        public bool TryGetId( out int packetId, [NotNullWhen( true )] out Task<object?>? idFreedAwaiter )
        {
            lock( _entries )
            {
                if( _nextFreeId == 0 )
                {
                    if( _count == _maxPacketId )
                    {
                        packetId = 0;
                        idFreedAwaiter = null;
                        return false;
                    }
                    EnsureSlotsAvailable( ++_count );
                    packetId = _count;
                }
                else
                {
                    packetId = _nextFreeId;
                    _nextFreeId = _entries[packetId - 1].NextFreeId;
                }
                var tcs = new TaskCompletionSource<object?>();
                _entries[packetId - 1].TaskCS = tcs;
                idFreedAwaiter = tcs.Task;
                return true;
            }
        }

        public bool FreeId( int packetId, object? result = null )
        {
            lock( _entries )
            {
                _entries[packetId - 1].NextFreeId = _nextFreeId;
                _nextFreeId = packetId;
                var tcs = _entries[packetId - 1].TaskCS;
                if( tcs == null ) return false;
                tcs.SetResult( result );
                _entries[packetId - 1].TaskCS = null;
                return true;
            }
        }

        public void EnsureSlotsAvailable( int count )
        {
            if( _entries.Length < count )
            {
                Entry[] newEntries = new Entry[count * 2];
                _entries.CopyTo( newEntries, 0 );
                _entries = newEntries;
            }
        }
    }
}
