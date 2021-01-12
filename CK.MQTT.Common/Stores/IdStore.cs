using System;
using System.Diagnostics;

#nullable enable

namespace CK.MQTT
{
    class IdStore<T> where T : notnull
    {
        // This is a doubly linked list.
        // There is a cursor, named '_oldestIdAllocated' that point to the oldest ID allocated.
        // Packet behind this cursor are available packet ID.
        // Packet after this cursor are used packets ID.
        // Because we prepend the list when an ID is freed and append when an ID is used it mean that the IDs are sorted chronogically:
        // - The tail is the last freed packet ID,
        // - The previous packet of the "_oldestIdAllocated" is the oldest available ID.
        // - The _oldestIdAllocated, is well, the oldest id allocated.
        // - The head of the list is the newest id allocated.

        internal struct Entry
        {
            public int NextId;
            public int PreviousId;
            public T Content;
        }

        internal Entry[] _entries = new Entry[64];
        int _oldestIdAllocated = 0;
        int _tail = 0;
        int _head = 0;
        /// <summary>
        /// Current count of Entries.
        /// </summary>
        int _count = 1;
        readonly int _maxPacketId;

        public IdStore( int packetIdMaxValue ) => _maxPacketId = packetIdMaxValue;

        internal bool CreateNewId( out int packetId, out T? result )
        {
            ref Entry oldHead = ref _entries[_head - 1];
            if( _oldestIdAllocated == _tail ) // The oldest packet we sent is also the tail. It mean there are no packet Id available.
            {
                if( _count == _maxPacketId ) // All packets are busy.
                {
                    packetId = 0;
                    result = default;
                    return false;
                }
                EnsureSlotsAvailable( ++_count ); // We create space if required to store this id.
                packetId = _count; // Now we can use this id.
                ref Entry newEntry = ref _entries[packetId - 1];
                newEntry = new Entry
                {
                    PreviousId = _head, // Previous head is our previous.
                    NextId = 0 // It's the head, so 0.
                };
                oldHead.NextId = packetId; // We set the previous head next.
                _head = packetId; // The head is now this packet
                result = newEntry.Content;
                return true;
            }
            ref Entry oldestId = ref _entries[_oldestIdAllocated - 1];
            packetId = oldestId.PreviousId; // We take the oldest unused packet id.
            Debug.Assert( packetId != 0, "We didn't used all the IDs so at least one should be available." );
            int previous = _entries[packetId - 1].PreviousId;
            if( previous != 0 )
            {
                _entries[previous - 1].NextId = _oldestIdAllocated;
                oldestId.PreviousId = previous;
            }
            else
            {
                _entries[_oldestIdAllocated - 1].PreviousId = 0;
                // TODO: Theorical scenario: Is allocating there faster since we don't need to access immediatly allocated memory ?
            }
            oldHead.NextId = packetId;
            _entries[packetId - 1].NextId = 0;
            _entries[packetId - 1].PreviousId = _head;
            _head = packetId;
            result = _entries[packetId - 1].Content;
            if( _tail == packetId ) _tail = _oldestIdAllocated;
            return true;
        }

        internal void FreeId( IInputLogger? m, int packetId )
        {
#if DEBUG
            int curr = packetId;
            while( curr != _oldestIdAllocated )
            {
                curr = _entries[packetId - 1].PreviousId;
                if( curr == 0 ) throw new InvalidOperationException( "Id was not allocated." );
            }
#endif
            _entries[packetId - 1].NextId = _tail;
            _entries[packetId - 1].PreviousId = 0;
            _entries[_tail - 1].PreviousId = packetId;
            _tail = packetId;
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

        internal void Reset()
        {
            if( _count == 0 ) return;
            _count = 1;
            Array.Clear( _entries, 0, _entries.Length );
            _tail = 0;
            _head = 0;
            _oldestIdAllocated = 0;
        }

        internal bool Empty => _count == 0;
    }
}
