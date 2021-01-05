using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

#nullable enable

namespace CK.MQTT
{
    [DebuggerDisplay( "Count = {_count} {DebuggerDisplay}" )]
    internal class IdStore<T> where T : notnull
    {
        struct Entry
        {
            public int NextFreeId;
            public T Content;
        }

        Entry[] _entries = new Entry[64];
        int _nextFreeId = 0;
        /// <summary>
        /// Current count of Entries.
        /// </summary>
        int _count = 0;
        readonly int _maxPacketId;
        string DebuggerDisplay => string.Concat( _entries.Select( s => s!.ToString() ) );

        public IdStore( int packetIdMaxValue ) => _maxPacketId = packetIdMaxValue;

        internal bool TryGetById( out int packetId, [NotNullWhen( true )] out T? result )
        {
            lock( _entries )
            {
                if( _nextFreeId == 0 ) // When 0 it mean there are no id
                {
                    if( _count == _maxPacketId )
                    {
                        packetId = 0;
                        result = default;
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
                result = _entries[packetId - 1].Content;
                return true;
            }
        }

        internal void FreeId( IInputLogger? m, int packetId )
        {
            _entries[packetId - 1] = default; // I hope it's faster than zero'ing everything by hand. Will be more change-proof anyway.
            _entries[packetId - 1].NextFreeId = _nextFreeId;
            _nextFreeId = packetId;
            m?.FreedPacketId( packetId );// This may want to free the packet we are freeing. So it must be ran after the free process.
        }

        void EnsureSlotsAvailable( int count )
        {
            if( _entries.Length < count )
            {
                int newCount = count * 2;
                if( count * 2 > _maxPacketId ) newCount = _maxPacketId;
                Entry[] newEntries = new Entry[newCount];
                _entries.CopyTo( newEntries, 0 );
                _entries = newEntries;
            }
        }

        IEnumerable<T> GetEntries()
        {

        }

        internal void Reset()
        {
            lock( _entries )
            {
                if( _count == 0 ) return;
                _count = 0;
                Array.Clear( _entries, 0, _entries.Length );
                _nextFreeId = 1;
            }
        }

        internal bool Empty => _count == 0;
    }
}
