using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT.Common
{
    internal class AckManager
    {
        readonly Stopwatch _stopwatch = new Stopwatch();
        readonly List<ValueTuple<int, long>> _packets = new List<ValueTuple<int, long>>();
        public AckManager()
            => _stopwatch.Start();

        internal void SendPacket( int packetId )
        {
            lock( _packets )
            {
                _packets.Add( new ValueTuple<int, long>( packetId, _stopwatch.ElapsedMilliseconds ) );
            }
        }

        internal void DiscardPacket( int packetId )
        {
            lock( _packets )
            {
                _packets.RemoveAt( _packets.IndexOf( p => p.Item1 == packetId ) );
            }
        }

        /// <summary>
        /// Returned packet are also forgotten from the <see cref="AckManager"/>.
        /// </summary>
        /// <param name="waitTimeSecs"></param>
        /// <returns></returns>
        internal IEnumerable<int> PacketWaitingForMoreThan( int waitTimeSecs )
        {
            lock( _packets )
            {
                long dueFor = _stopwatch.ElapsedMilliseconds - waitTimeSecs * 1000;
                return _packets.RemoveWhereAndReturnsRemoved( s => s.Item2 < dueFor ).Select( s => s.Item1 );
            }
        }

        internal long TimeUntilAPacketWaitForMoreThan( int waitTimeSecs )
            => _packets.Min( s => s.Item2 ) + waitTimeSecs * 1000 - _stopwatch.ElapsedMilliseconds;
    }
}
