using System;
using System.Collections.Generic;
using System.Text;

namespace CK.MQTT.Common.Stores
{
    class Temp
    {
        //REDO LOCK.

        [DebuggerDisplay( "{DebuggerDisplay} {NextFreeId}" )]
        struct Entry
        {
            public TaskCompletionSource<object?>? TaskCS;
            public TimeSpan EmissionTime;
            public int NextFreeId;
            public ushort TryCount;

            public bool Acked => TaskCS == null;
            public char DebuggerDisplay => EmissionTime == default ? '-' : Acked ? '?' : 'X';
        }
        

        void CleanUncertainPacketId( IInputLogger? m, TimeSpan timeSpan )
        {
            if( !_haveUncertainPacketId ) return;
            _haveUncertainPacketId = false;
            for( ushort i = 0; i < _entries.Length; i++ )
            {
                Entry entry = _entries[i];
                if( entry.TryCount > 1 && entry.Acked )
                {
                    if( entry.EmissionTime < timeSpan )
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

        public void CancelAllAcks( IActivityMonitor? m )
        {
            lock( _entries )
            {
                using( m?.OpenTrace( "Cancelling all ack's tasks." ) )
                {
                    for( int i = 0; i < _entries.Length; i++ )
                    {
                        m?.Trace( $"Cancelling task for packet id {i + 1}." );
                        _entries[i].TaskCS!.SetCanceled();
                    }
                }
            }
        }

        public (int packetId, int waitTime) GetOldestUnackedPacket()
        {
            lock( _entries )
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
                        && !entry.Acked
                        && entry.EmissionTime <= smallest.EmissionTime
                        && (entry.TryCount != confTryCount || confTryCount == 0) )
                    {
                        smallest = entry;
                        smallIndex = i;
                    }
                }
                return (smallIndex + 1, (int)((_stopwatch.Elapsed - smallest.EmissionTime).Ticks / TimeSpan.TicksPerMillisecond));
            }
        }
    }
}
