using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <typeparam name="T">Dispose may be called multiple times.</typeparam>
    class MqttIdStore<T> where T : IDisposable
    {
        // * Lot of important logic happen here:
        //   - When a packet ID is freed. (Not as simple as it seems)
        //   - Detecting that a packet has been dropped by the network.
        //   - When the data of a Packet should be disposed.
        //
        //
        // * About "uncertain dead" packet:
        //   1. A sender try to send a packet with ID 1.
        //      But due to an ongoing event, the network is not stable, and drop this packet.
        //   2. The sender retry to send this packet, so we set the dup flag.
        //      Because of the ongoing event, the latency is increased, but the packet is not dropped.
        //   3. Due to the increased latency, the sender retry to send this packet.
        //   4. The sender now receive an ack for the ID 1.
        //   We have no way to know that this ack is from the first, or second resend.
        //   It mean, we could receive soon a second packet with the same ID.
        //   This second packet is like the Schrodinger's cat.
        //   We can directly observe that the packet is alive, when we receive it.
        //   But we can't directly observe that the packet is dead.
        //   But we can observe indirectly it's death, because MQTT specify that:
        //      - The underlying network MUST respect the packets orders.
        //      - The receiver MUST respond in the same order it received the message.
        //   If we observe an Ack for any message that was sent after the last retry,
        //      we indirectly observed the packet death, because we should had received it before.
        //   
        //   So, contrary to what the MQTT specs says, in this case we SHOULD NOT free the ID right after the ack reception.
        enum QoSState : byte
        {
            /// <summary>
            /// Packet was never acked yet.
            /// </summary>
            NeverAcked = 0,
            QoS1 = 1 << 0,
            QoS2 = 1 << 1,
            QoS2PubRec = 1 << 1,
            Dropped = 1 << 6,
            UncertainDead = 1 << 7
        }
        struct EntryContent
        {
            public TimeSpan LastEmissionTime;
            public TaskCompletionSource<object?> TaskCompletionSource;
            public T Storage;
            public byte AttemptInTransitOrLost;
            public QoSState State;
        }

        readonly IdStore<EntryContent> _idStore;
        readonly IStopwatch _stopwatch;
        public MqttIdStore( int packetIdMaxValue, MqttConfigurationBase config )
        {
            _idStore = new IdStore<EntryContent>( packetIdMaxValue );// TODO: make a start size of 32.
            _stopwatch = config.StopwatchFactory.Create();
        }

        public bool SendingPacketFirstTime( IOutputLogger? m, QualityOfService qos, out int packetId, [MaybeNullWhen( false )] out Task ackTask )
        {
            Debug.Assert( qos != QualityOfService.AtMostOnce );
            lock( _idStore )
            {
                bool res = _idStore.CreateNewId( out packetId, out EntryContent entry );
                if( !res ) &
                {
                    ackTask = null;
                    return false;
                }
                entry.LastEmissionTime = _stopwatch.Elapsed;
                entry.AttemptInTransitOrLost = 1;
                TaskCompletionSource<object?> tcs = new();
                entry.TaskCompletionSource = tcs;
                ackTask = tcs.Task;
                return true;
            }
        }

        public void PacketResent( IOutputLogger? m, int packetId )
        {
            _idStore._entries[packetId - 1].Content.AttemptInTransitOrLost++;
            _idStore._entries[packetId - 1].Content.LastEmissionTime = _stopwatch.Elapsed;
        }

        /// <summary>
        /// The first ack in the protocol steps.
        /// Doesn't mean it's the first ack received but it's the first ack in the workflow.
        /// </summary>
        /// <param name="m"></param>
        /// <param name="packetId"></param>
        /// <param name="taskCompletionSource"></param>
        public void PacketAck( IInputLogger? m, int packetId, bool qos2PubRec, out TaskCompletionSource<object?> taskCompletionSource )
        {
            ref IdStore<EntryContent>.Entry entry = ref _idStore._entries[packetId - 1];

            // Whatever the QoS, we can:
            taskCompletionSource = entry.Content.TaskCompletionSource; // Set the corresponding task source.
            entry.Content.Storage.Dispose(); // Dispose data
            QoSState state = entry.Content.State;
            Debug.Assert( (state & QoSState.Dropped) != QoSState.Dropped );

            int currId = packetId;
            ref var curr = ref _idStore._entries[currId - 1];
            while( currId != _idStore._oldestIdAllocated ) // We loop over all older packets.
            {
                currId = curr.PreviousId;
                curr = ref _idStore._entries[currId - 1];

                if( curr.Content.LastEmissionTime < entry.Content.LastEmissionTime )
                {
                    // The last retry of this packet occured before the current packet was sent.
                    // We can assert that this ack packet was dropped.
                    if( (curr.Content.State & QoSState.UncertainDead) == QoSState.UncertainDead )
                    {
                        // If the packet was in an uncertain state, it mean the ack logic ran.
                        // But we could not knew if there was another ack in the pipe or not.
                        // No we know that no such ack is in transit.
                        // So we can simply free it now.
                        _idStore.FreeId( m, currId );
                    }
                    else
                    {
                        curr.Content.State |= QoSState.Dropped; // We mark the packet as dropped so it can be resent immediatly.
                    }
                }
            }


            QualityOfService qos = (QualityOfService)((byte)state & (byte)QualityOfService.Mask);
            Debug.Assert( qos != QualityOfService.AtMostOnce );


            if( qos == QualityOfService.AtLeastOnce )
            {
                if( entry.Content.AttemptInTransitOrLost > 1 )
                {
                    entry.Content.AttemptInTransitOrLost--;
                    entry.Content.State = QoSState.UncertainDead;
                }
                else
                {
                    _idStore.FreeId( m, packetId );
                }
            }
            else
            {
                Debug.Assert( qos == QualityOfService.ExactlyOnce );
                // TODO: we dont have info: which QoS2 packet we received ?
                if( !qos2PubRec )
                {
                    if( entry.Content.AttemptInTransitOrLost > 1 )
                    {
                        entry.Content.AttemptInTransitOrLost--;
                        entry.Content.State = QoSState.UncertainDead;
                    }
                    else
                    {
                        _idStore.FreeId( m, packetId );
                    }
                }
                else if( (entry.Content.State & QoSState.QoS2PubRec) != QoSState.QoS2PubRec )
                {
                    // First encounter of the PubRec.
                    entry.Content.AttemptInTransitOrLost = 0; // We set to zero because we don't care of the uncertain logic here. The next ack in the process will "clean" the pipe.
                    entry.Content.State |= QoSState.QoS2PubRec;
                }
            }
        }
    }
}
