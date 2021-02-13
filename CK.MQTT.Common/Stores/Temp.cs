using CK.Core;
using System;

namespace CK.MQTT.Common.Stores
{
    class Temp
    {
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
                IdStoreEntry smallest = new IdStoreEntry
                {
                    EmissionTime = TimeSpan.MaxValue
                };
                // If not assigned, will return 0 (invalid packet id).
                int smallIndex = -1;
                ushort confTryCount = _config.AttemptCountBeforeGivingUpPacket;
                for( int i = 0; i < _count; i++ )
                {
                    IdStoreEntry entry = _entries[i];
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
