using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT
{
    class MqttIdStore<T>
    {
        enum QoSState : byte
        {
            /// <summary>
            /// Packet was never acked yet.
            /// </summary>
            NeverAcked = 0,
            QoS1 = 1,
            QoS2 = 2,
            QoS1Completed,

        }
        struct Entry
        {
            public TimeSpan EmissionTime;
            public TaskCompletionSource<object?> TaskCompletionSource;
            public T Storage;
            public byte TryCount;
            public byte State;
        }

        /// <summary>
        /// TODO:
        /// Can the buggy scenario happen in QoS 2 ?
        /// Maybe merge fields to use less bytes ?
        /// </summary>

        readonly IdStore<Entry> _idStore;
        bool _haveUncertainPacketId;
        readonly IStopwatch _stopwatch;
        public MqttIdStore( int packetIdMaxValue, MqttConfigurationBase config )
        {
            _idStore = new IdStore<Entry>( packetIdMaxValue );
            _stopwatch = config.StopwatchFactory.Create();

        }

        public bool SendingPacketFirstTime( IOutputLogger? m, QualityOfService qos, out int packetId, [MaybeNullWhen( false )] out Task ackTask )
        {
            Debug.Assert( qos != QualityOfService.AtMostOnce );
            lock( _idStore )
            {
                bool res = _idStore.CreateNewId( out packetId, out Entry entry );
                if( !res )
                {
                    ackTask = null;
                    return false;
                }
                entry.EmissionTime = _stopwatch.Elapsed;
                entry.TryCount = 1;
                TaskCompletionSource<object?> tcs = new();
                entry.TaskCompletionSource = tcs;
                ackTask = tcs.Task;
                return true;
            }
        }

        public void PacketResent( IOutputLogger? m, int packetId )
        {
            _idStore._entries[packetId - 1].Content.TryCount++;
            _haveUncertainPacketId = true;
        }

        public void PacketAcked( IInputLogger? m, int packetId )
        {
            ref Entry entry = ref _idStore._entries[packetId].Content;
            byte state = entry.State;
            QualityOfService qos = (QualityOfService)(state & (byte)QualityOfService.Mask);
            if( qos == QualityOfService.AtLeastOnce )
            {
                if(_idStore._entries)
            }
        }
    }
}
