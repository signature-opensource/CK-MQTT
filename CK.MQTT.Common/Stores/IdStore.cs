using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace CK.MQTT
{
    [DebuggerDisplay( "Count = {_count} {DebuggerDisplay}" )]
    class IdStore
    {
        [DebuggerDisplay( "{DebuggerDisplay} {NextFreeId,nq}" )]
        struct Entry
        {
            public int NextFreeId;
            public TaskCompletionSource<object?>? TaskCS;

            public char DebuggerDisplay => TaskCS == null ? '-' : 'X';
        }

        Entry[] _entries;
        int _nextFreeId = 0;
        /// <summary>
        /// Current count of Entries.
        /// </summary>
        int _count = 0;
        readonly int _maxPacketId;

        string DebuggerDisplay
        {
            get
            {
                Span<char> chars = new char[_entries.Length];
                for( int i = 0; i < _entries.Length; i++ ) chars[i] = _entries[i].DebuggerDisplay;
                return chars.ToString();
            }
        }

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
            TaskCompletionSource<object?>? tcs;
            lock( _entries )
            {
                _entries[packetId - 1].NextFreeId = _nextFreeId;
                _nextFreeId = packetId;
                tcs = _entries[packetId - 1].TaskCS;
                if( tcs == null ) return false;
                _entries[packetId - 1].TaskCS = null;
            }
            tcs.SetResult( result );//This must be the last thing we do, the tcs.SetResult may be a code calling
            return true;
        }

        void EnsureSlotsAvailable( int count )
        {
            if( _entries.Length < count )
            {
                Entry[] newEntries = new Entry[count * 2];
                _entries.CopyTo( newEntries, 0 );
                _entries = newEntries;
            }
        }

        public void Reset()
        {
            if( _count == 0 ) return;
            lock( _entries )
            {
                _count = 0;
                Array.Clear( _entries, 0, _entries.Length );
                _entries[0] = new Entry();
                _nextFreeId = 1;
            }
        }

        public bool Empty => _count == 0;
    }
}
