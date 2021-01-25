using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    /// <typeparam name="T">Dispose may be called multiple times.</typeparam>
    public abstract class MqttIdStore<T>
    {
        // * Lot of important logic happen here:
        //   - When a packet ID is freed. (Not as simple as it seems)
        //   - Detecting that a packet has been dropped by the network.
        //   - When the data of a Packet should be stored/disposed.
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
        protected enum QoSState : byte
        {
            QoS1 = 1 << 0,
            QoS2 = 1 << 1,
            QoS2PubRecAcked = 1 << 2,
            Dropped = 1 << 6,
            UncertainDead = 1 << 7,
            PacketAckedMask = QoS2PubRecAcked | UncertainDead
        }
        protected struct EntryContent
        {
            internal TimeSpan _lastEmissionTime;
            public T Storage;
            internal byte _attemptInTransitOrLost;
            internal QoSState _state;
            internal TaskCompletionSource<object?> _taskCompletionSource;
        }

        readonly IdStore<EntryContent> _idStore;
        readonly IStopwatch _stopwatch;
        TaskCompletionSource<object?>? _idFullTCS = null; //TODO: replace by non generic TCS in .NET 5
        public MqttIdStore( int packetIdMaxValue, MqttConfigurationBase config )
        {
            _idStore = new( packetIdMaxValue, config.IdStoreStartCount );
            _stopwatch = config.StopwatchFactory.Create();
        }

        protected ref T this[int packetId] => ref _idStore._entries[packetId - 1].Content.Storage;

        protected abstract ValueTask StorePacket( IOutputLogger? m, IOutgoingPacketWithId content );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="m"></param>
        /// <param name="content"></param>
        /// <param name="qos"></param>
        /// <returns><see langword="null"/> when no packet id was available.</returns>
        public async ValueTask<Task<object?>> SendingPacketFirstTime( IOutputLogger? m, IOutgoingPacketWithId content, QualityOfService qos )
        {
            Debug.Assert( qos != QualityOfService.AtMostOnce );
            int packetId;
            EntryContent entry;
            bool res = false;
            lock( _idStore )
            {
                res = _idStore.CreateNewId( out packetId, out entry );
            }
            while( !res )
            {
                lock( _idStore )
                {
                    res = _idStore.CreateNewId( out packetId, out entry );
                    _idFullTCS = new TaskCompletionSource<object?>();
                }
                await _idFullTCS.Task; // Asynchronously wait that a new packet id is available.
            }

            // We don't need the lock there, packet is not sent yet so we wont receive an ack for this ID.
            content.PacketId = packetId;
            entry._lastEmissionTime = _stopwatch.Elapsed;
            entry._attemptInTransitOrLost = 1;
            TaskCompletionSource<object?> tcs = new();
            entry._taskCompletionSource = tcs;
            await StorePacket( m, content );
            return tcs.Task;
        }

        public void PacketResent( IOutputLogger? m, int packetId )
        {
            lock( _idStore )
            {
                _idStore._entries[packetId - 1].Content._attemptInTransitOrLost++;
                _idStore._entries[packetId - 1].Content._lastEmissionTime = _stopwatch.Elapsed;
            }
        }

        protected abstract ValueTask RemovePacket( IInputLogger? m, int packetId );

        /// <summary>
        /// The first ack in the protocol steps.
        /// Doesn't mean it's the first ack received but it's the first ack in the workflow.
        /// </summary>
        /// <param name="m"></param>
        /// <param name="packetId"></param>
        /// <param name="taskCompletionSource"></param>
        public async ValueTask PacketAck( IInputLogger? m, int packetId, bool qos2PubRec )
        {
            QoSState state = _idStore._entries[packetId - 1].Content._state;
            Debug.Assert( (state & QoSState.Dropped) != QoSState.Dropped );
            if( (byte)(state & QoSState.PacketAckedMask) > 0 )
            {
                await RemovePacket( m, packetId );
            }
            DoPacketAck( m, state, packetId, qos2PubRec );
        }
        void FreeId( IInputLogger? m, int packetId )
        {
            lock( _idStore )
            {
                _idStore.FreeId( m, packetId );
                _idFullTCS?.SetResult( null );
            }
        }

        void DoPacketAck( IInputLogger? m, QoSState state, int packetId, bool qos2PubRec )
        {
            ref IdStore<EntryContent>.Entry entry = ref _idStore._entries[packetId - 1];


            int currId = packetId;
            ref var curr = ref _idStore._entries[currId - 1];
            while( currId != _idStore._oldestIdAllocated ) // We loop over all older packets.
            {
                currId = curr.PreviousId;
                curr = ref _idStore._entries[currId - 1];

                if( curr.Content._lastEmissionTime < entry.Content._lastEmissionTime )
                {
                    // The last retry of this packet occured before the current packet was sent.
                    // We can assert that this ack packet was dropped.
                    if( (curr.Content._state & QoSState.UncertainDead) == QoSState.UncertainDead )
                    {
                        // If the packet was in an uncertain state, it mean the ack logic ran.
                        // But we could not knew if there was another ack in the pipe or not.
                        // No we know that no such ack is in transit.
                        // So we can simply free it now.
                        FreeId( m, currId );
                    }
                    else
                    {
                        curr.Content._state |= QoSState.Dropped; // We mark the packet as dropped so it can be resent immediatly.
                    }
                }
            }

            QualityOfService qos = (QualityOfService)((byte)state & (byte)QualityOfService.Mask);
            Debug.Assert( qos != QualityOfService.AtMostOnce );

            if( qos == QualityOfService.AtLeastOnce )
            {
                if( entry.Content._attemptInTransitOrLost > 1 )
                {
                    entry.Content._attemptInTransitOrLost--;
                    entry.Content._state = QoSState.UncertainDead;
                }
                else
                {
                    FreeId( m, packetId );
                }
            }
            else
            {
                Debug.Assert( qos == QualityOfService.ExactlyOnce );
                // TODO: we dont have info: which QoS2 packet we received ?
                if( !qos2PubRec )
                {
                    if( entry.Content._attemptInTransitOrLost > 1 )
                    {
                        entry.Content._attemptInTransitOrLost--;
                        entry.Content._state = QoSState.UncertainDead;
                    }
                    else
                    {
                        FreeId( m, packetId );
                    }
                }
                else if( (entry.Content._state & QoSState.QoS2PubRecAcked) != QoSState.QoS2PubRecAcked )
                {
                    // First encounter of the PubRec.
                    entry.Content._attemptInTransitOrLost = 1; // We set to one because we don't care of the uncertain logic here. The next ack in the process will "clean" the pipe.
                    entry.Content._state |= QoSState.QoS2PubRecAcked;
                }
            }
        }
    }
}
