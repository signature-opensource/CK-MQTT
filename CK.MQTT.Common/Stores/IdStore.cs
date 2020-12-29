using CK.Core;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;

namespace CK.MQTT
{
    [DebuggerDisplay( "Count = {_count} {DebuggerDisplay}" )]
    public class IdStore
    {
        [DebuggerDisplay( "{DebuggerDisplay} {NextFreeId}" )]
        struct Entry
        {
            public int NextFreeId;
            public ushort TryCount;
            /// <summary>
            /// <see langword="null"/> null when the entry is free, or the packet is in an uncertain state.
            /// </summary>
            public TaskCompletionSource<object?>? TaskCS;
            public TimeSpan EmissionTime;
            public char DebuggerDisplay => TaskCS == null ? '-' : 'X'; //Todo: change and show incertain state
        }

        Entry[] _entries;
        int _nextFreeId = 0;
        /// <summary>
        /// Current count of Entries.
        /// </summary>
        int _count = 0;
        bool _haveUncertainPacketId;
        readonly int _maxPacketId;
        readonly MqttConfigurationBase _config;
        readonly IStopwatch _stopwatch;
        string DebuggerDisplay
        {
            get
            {
                Span<char> chars = new char[_entries.Length];
                for( int i = 0; i < _entries.Length; i++ ) chars[i] = _entries[i].DebuggerDisplay;
                return chars.ToString();
            }
        }

        public IdStore( int packetIdMaxValue, MqttConfigurationBase config )
        {
            (_maxPacketId, _config) = (packetIdMaxValue, config);
            _entries = new Entry[64];
            _stopwatch = config.StopwatchFactory.Create();
            _stopwatch.Start();
        }

        public bool TryGetId( out int packetId, [NotNullWhen( true )] out Task<object?>? idFreedAwaiter )
        {
            lock( _entries )
            {
                if( _nextFreeId == 0 ) // When 0 it mean there are no 
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

        public void SendingPacket( IOutputLogger? m, int packetId )
        {
            lock( _entries )
            {
                TimeSpan elapsed = _stopwatch.Elapsed;
                // I want to avoid having a TimeSpan being equal to default, which mean the value is uninitialized.
                _entries[packetId - 1].EmissionTime = elapsed.Ticks > 0 ? elapsed : new TimeSpan( 1 );
                int count = ++_entries[packetId - 1].TryCount;
                if( count > 1 )
                {
                    _haveUncertainPacketId = true;
                }
                if( count == _config.AttemptCountBeforeGivingUpPacket && count > 0 )
                {
                    _entries[packetId - 1].TaskCS!.SetCanceled();
                    m?.PacketMarkedPoisoned( packetId, count );
                }
            }
        }

        void CleanUncertainPacketId( IInputLogger? m, TimeSpan timeSpan )
        {
            if( !_haveUncertainPacketId ) return;
            _haveUncertainPacketId = false;
            for( ushort i = 0; i < _entries.Length; i++ )
            {
                Entry entry = _entries[i];
                if( entry.TryCount > 1 && entry.TaskCS == null )
                {
                    if( entry.EmissionTime > timeSpan )
                    {
                        FreeId( m, i + 1 );
                    }
                    else
                    {
                        _haveUncertainPacketId = true;
                    }
                }
            }
        }

        public void OnAck( IInputLogger? m, int packetId, object? result = null )
        {
            lock( _entries )
            {
                if( packetId > _count ) throw new ProtocolViolationException( "The sender sent a packet id that does not exist." );
                Entry entry = _entries[packetId - 1];
                if( entry.EmissionTime == default )
                {
                    if( entry.TaskCS != null ) throw new InvalidOperationException( "Store did not knew that this packet was sent." );
                    throw new ProtocolViolationException( $"Ack of an unknown packet id '{packetId}'." );
                }
                // Considering the following scenario:
                // The line latency is really high.
                // We send SUBSCRIBE packetId: 1
                // We resend SUBSCRIBE packetId: 1
                // We receive SUBACK packetId: 1.
                // Right now, there may be another SUBACK incoming, so we must not free the packetId 1 right now.
                // YES, the MQTT spec says that you can reuse it now, but the spec is wrong.
                CleanUncertainPacketId( m, _entries[packetId - 1].EmissionTime );
                if( entry.TryCount > 1 )
                {
                    _entries[packetId - 1].TaskCS = null; //Setting this to null allow to mark the packet as "uncertain".
                    _haveUncertainPacketId = true;
                }
                else
                {
                    FreeId( m, packetId );
                }
                entry.TaskCS!.SetResult( result );
            }
        }

        public void CancelAllAcks( IActivityMonitor m )
        {
            lock( _entries )
            {
                using( m.OpenTrace( "Cancelling all ack's tasks." ) )
                {
                    for( int i = 0; i < _entries.Length; i++ )
                    {
                        m.OpenTrace( $"Cancelling task for packet id {i + 1}." );
                        _entries[i].TaskCS!.SetCanceled();
                    }
                }
            }
        }

        void FreeId( IInputLogger? m, int packetId )
        {
            _entries[packetId - 1] = default; // I hope it's faster than zero'ing everything by hand. Will be more change-proof anyway.
            _entries[packetId - 1].NextFreeId = _nextFreeId;
            _nextFreeId = packetId;
            m?.FreedPacketId( packetId );// This may want to free the packet we are freeing. So it must be ran after the free process.
        }

        public (int packetId, int waitTime) GetOldestPacket()
        {
            Entry smallest = new Entry
            {
                EmissionTime = TimeSpan.MaxValue
            };
            // If not assigned, will return 0 (invalid packet id).
            int smallIndex = -1;
            ushort confTryCount = _config.AttemptCountBeforeGivingUpPacket;
            for( int i = 0; i < _count; i++ )
            {
                Entry entry = _entries[i];
                if( entry.EmissionTime != default
                    && entry.EmissionTime <= smallest.EmissionTime
                    && (entry.TryCount != confTryCount || confTryCount == 0) )
                {
                    smallest = entry;
                    smallIndex = i;
                }
            }
            return (smallIndex + 1, (int)((_stopwatch.Elapsed - smallest.EmissionTime).Ticks / TimeSpan.TicksPerMillisecond));
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
            lock( _entries )
            {
                if( _count == 0 ) return;
                _count = 0;
                Array.Clear( _entries, 0, _entries.Length );
                _nextFreeId = 1;
            }
        }

        public bool Empty => _count == 0;
    }
}
