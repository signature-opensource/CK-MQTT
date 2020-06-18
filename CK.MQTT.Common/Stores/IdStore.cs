using System;
using System.Collections.Generic;
using System.Text;
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
        int _count;
        readonly int _maxPacketId;

        public IdStore( int packetIdMaxValue )
        {
            _entries = new Entry[64];
            _maxPacketId = packetIdMaxValue;
        }

        public int GetId()
        {
            lock( _entries )
            {

                int packetId;
                if( _nextFreeId == 0 )
                {
                    if( _count == _maxPacketId ) return 0;
                    EnsureSlotsAvailable( ++_count );
                    packetId = _count;
                    _entries[packetId - 1].TaskCS = new TaskCompletionSource<object?>();
                }
                else
                {
                    packetId = _nextFreeId;
                    _nextFreeId = _entries[packetId - 1].NextFreeId;
                }
                return packetId;
            }
        }

        public Task<object?>? GetAwaiterById( int packetId )
        {
            lock( _entries )
            {
                return _entries[packetId].TaskCS?.Task;
            }
        }

        public bool SetResultById(int packetId, object? result)
        {
            var tcs = _entries[packetId].TaskCS;
            if( tcs == null ) return false;
            tcs.SetResult( result );
            return true;
        }

        public bool FreeId( int packetId, object? result = null )
        {
            lock( _entries )
            {
                _entries[packetId].NextFreeId = _nextFreeId;
                _nextFreeId = packetId;
                var tcs = _entries[packetId].TaskCS;
                if( tcs == null ) return false;
                tcs.SetResult( result );
                _entries[packetId].TaskCS = null;
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
