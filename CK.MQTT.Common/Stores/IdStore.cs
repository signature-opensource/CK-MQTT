using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
            public ushort TryCount;
            public TaskCompletionSource<object?>? TaskCS;
            public long EmissionTime;
            public char DebuggerDisplay => TaskCS == null ? '-' : 'X';
        }

        Entry[] _entries;
        int _nextFreeId = 0;
        /// <summary>
        /// Current count of Entries.
        /// </summary>
        int _count = 0;
        readonly int _maxPacketId;
        readonly MqttConfiguration _config;
        readonly Stopwatch _stopwatch = new Stopwatch();
        string DebuggerDisplay
        {
            get
            {
                Span<char> chars = new char[_entries.Length];
                for( int i = 0; i < _entries.Length; i++ ) chars[i] = _entries[i].DebuggerDisplay;
                return chars.ToString();
            }
        }

        public IdStore( int packetIdMaxValue, MqttConfiguration config )
        {
            _entries = new Entry[64];
            _maxPacketId = packetIdMaxValue;
            _config = config;
            _config = config;
            _stopwatch.Start();
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
        public void PacketSent( IMqttLogger m, int packetId )
        {
            lock( _entries )
            {
                _entries[packetId - 1].EmissionTime = _stopwatch.ElapsedMilliseconds;
                int count = ++_entries[packetId - 1].TryCount;
                if( count == _config.AttemptCountBeforeGivingUpPacket && _config.AttemptCountBeforeGivingUpPacket > 0 )
                {
                    m.Warn( $"Packet with id {packetId} is not acknowledged after sending it {count - 1} times." +
                        $"\nThis was the last attempt, as configured." );
                }
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
                _entries[packetId - 1].EmissionTime = 0;
                _entries[packetId - 1].TryCount = 0;
            }
            tcs.SetResult( result );//This must be the last thing we do, the tcs.SetResult may continue a Task synchronously.
            return true;
        }

        public (int packetId, long waitTime) GetOldestPacket()
        {
            Entry smallest = new Entry
            {
                EmissionTime = long.MaxValue
            };
            int smallIndex = -1;//if not assigned, will return 0 (invalid packet id)
            ushort confTryCount = _config.AttemptCountBeforeGivingUpPacket;
            for( int i = 0; i < _count; i++ )
            {
                Entry entry = _entries[i];
                if( entry.EmissionTime != 0
                    && entry.EmissionTime <= smallest.EmissionTime
                    && (entry.TryCount != confTryCount || confTryCount == 0) )
                {
                    smallest = entry;
                    smallIndex = i;
                }
            }
            return (smallIndex + 1, _stopwatch.ElapsedMilliseconds - smallest.EmissionTime);
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
