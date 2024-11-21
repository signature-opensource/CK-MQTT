using CK.MQTT.Common.Stores;
using System;
using System.Diagnostics;

#nullable enable

namespace CK.MQTT.Stores;

class IdStore<T> where T : struct
{
    // This is a doubly linked list.
    // There is a cursor, named '_newestIdAllocated' that point to the most recent ID allocated.
    // Packet behind this cursor are available packet ID.
    // Packet after this cursor are used packets ID.
    // Because we move an element to the tail when an ID is freed it mean that the IDs are sorted chronologically:
    // - The tail is the most recent freed packet ID,
    // - The previous packet of the "_newestIdAllocated" is the oldest freed ID.
    // - The _newestIdAllocated, is well, the newest id allocated.
    // - The head of the list is the oldest id allocated.
    // - To allocate a new ID we just have to move the _newestIdAllocated to the next element.


    /// <summary>
    /// o _head: oldest ID allocated  <br/>
    /// | <br/>
    /// | <br/>
    /// o _newestIdAllocated <br/>
    /// | ⬅️ This id was unallocated the longest ago<br/>
    /// | <br/>
    /// | ↖️ previous <br/>
    /// | ↙️ next <br/>
    /// | <br/>
    /// o _tail: most recent freed packet <br/>
    /// </summary>
    internal ArrayStartingAt1<IdStoreEntry<T>> _entries;
    /// <summary>
    /// When 0, all IDs are free.
    /// </summary>
    internal ushort _newestIdAllocated;
    /// <summary>
    /// The tail, also the most recent freed packet.
    /// </summary>
    internal ushort _tail;
    /// <summary>
    /// The head, also point to the newest ID allocated.
    /// </summary>
    internal ushort _head;
    readonly ushort _maxPacketId;

    public bool NoPacketAllocated
    {
        get
        {
            lock( _lock )
            {
                return _newestIdAllocated == 0;
            }
        }
    }

    readonly object _lock = new();

    public IdStore( ushort packetIdMaxValue, ushort startSize )
    {
        if( startSize <= 0 || startSize > packetIdMaxValue )
        {
            throw new ArgumentOutOfRangeException( $"{nameof( startSize )} must be greater than 0 and smaller than {packetIdMaxValue}" );
        }
        _maxPacketId = packetIdMaxValue;
        _entries = new ArrayStartingAt1<IdStoreEntry<T>>( new IdStoreEntry<T>[startSize] );
        Reset();
    }

    internal void Reset()
    {
        _entries.Clear();
        for( ushort i = 1; i < _entries.Length + 1; i++ )
        {
            _entries[i] = new IdStoreEntry<T>()
            {
                NextId = (ushort)(i + 1),
                PreviousId = (ushort)(i - 1) // 'i' is not incremented so it's the previous packet id.
            };
        }
        _entries[^0] = new IdStoreEntry<T>()
        {
            PreviousId = (ushort)(_entries.Length - 1)
        };
        _head = 1;
        _tail = (ushort)_entries.Length;
        _newestIdAllocated = 0;
    }


    internal void SelfConsistencyCheck()
    {
#if DEBUG
        lock( _lock )
        {
            uint curr = _head;
            uint count = 1;
            while( curr != _tail ) // forward
            {
                count++;
                curr = _entries[curr].NextId;
                if( curr == 0 ) throw new InvalidOperationException( "Error detected in the store: a node is not linked !" );
            }
            if( count != _entries.Length ) throw new InvalidOperationException( "Error detected in the store: links are not consistent." );
            count = 1;
            while( curr != _head ) // backward
            {
                count++;
                curr = _entries[curr].PreviousId;
                if( curr == 0 ) throw new InvalidOperationException( "Error detected in the store: a node is not linked !" );
            }
            if( count != _entries.Length ) throw new InvalidOperationException( "Error detected in the store: links are not consistent." );
        }
#endif
    }
    internal bool CreateNewEntry( T entry, out ushort packetId )
    {
        SelfConsistencyCheck();
        lock( _lock )
        {
            if( _newestIdAllocated == _tail ) // The oldest packet we sent is also the tail. It mean there are no packet Id available.
            {
                if( _entries.Length == _maxPacketId ) // All packets are busy.
                {
                    packetId = 0;
                    return false;
                }
                GrowEntriesArray();
                Debug.Assert( _newestIdAllocated != _tail );
            }
            if( _newestIdAllocated == 0 )
            {
                // Mean no ID is currently allocated.
                _newestIdAllocated = _head;
                packetId = _newestIdAllocated;
                _entries[_newestIdAllocated].Content = entry;
                return true;
            }
            packetId = _entries[_newestIdAllocated].NextId;
            _newestIdAllocated = packetId;
            _entries[_newestIdAllocated].Content = entry;
            SelfConsistencyCheck();
            return true;
        }
    }

    internal void FreeId( ushort packetId )
    {
        SelfConsistencyCheck();
        if( packetId == _newestIdAllocated )
        {
            // If a node have no previous, it equal to 0.
            // Here if the newest id allocated has no previous, it mean there a no other allocated id.
            // In this case, the _newestIdAllocated should be set to 0.
            _newestIdAllocated = _entries[packetId].PreviousId;
        }
        var next = _entries[packetId].NextId;
        var previous = _entries[packetId].PreviousId;
        // Removing the element from the linked list.
        if( packetId == _head )
        {
            lock( _lock )
            {
                _head = next;
            }
        }
        if( next != 0 )
        {
            _entries[next].PreviousId = previous;
        }
        if( previous != 0 )
        {
            _entries[previous].NextId = next;
        }

        _entries[packetId].NextId = 0;
        _entries[packetId].PreviousId = _tail;
        _entries[packetId].Content = default;
        _entries[_tail].NextId = packetId;
        _tail = packetId;
        SelfConsistencyCheck();
    }

    void GrowEntriesArray()
    {
        SelfConsistencyCheck();
        ushort newCount = (ushort)(_entries.Length * 2);
        // We dont want that the next grow make the array only 1 item bigger, it would cause a bug later in this function.
        if( newCount + 1 == _maxPacketId ) newCount = _maxPacketId;
        if( newCount > _maxPacketId ) newCount = _maxPacketId;
        var newEntries = new ArrayStartingAt1<IdStoreEntry<T>>( new IdStoreEntry<T>[newCount] );
        _entries.CopyTo( newEntries, 0 );
        for( ushort i = (ushort)(_entries.Length + 2); i < newEntries.Length; i++ ) // Link all the new entries.
        {
            newEntries[i] = new IdStoreEntry<T>()
            {
                PreviousId = (ushort)(i - 1),
                NextId = (ushort)(i + 1)
            };
        }
        newEntries[^0].PreviousId = (ushort)(newEntries.Length - 1);
        newEntries[_tail].NextId = (ushort)(_entries.Length + 1); // Link previous tail to our expansion.
        newEntries[_entries.Length + 1] = new IdStoreEntry<T>()
        {
            PreviousId = _tail, // Link expansion to previous tail.
            NextId = (ushort)(_entries.Length + 2) // If the count was increased only by 1, this would fail.
        };
        _tail = (ushort)newEntries.Length;
        _entries = newEntries;
        SelfConsistencyCheck();
    }
}
