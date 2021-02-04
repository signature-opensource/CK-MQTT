using CK.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Stores
{
    /// <typeparam name="T">Dispose may be called multiple times.</typeparam>
    public abstract class MqttIdStore<T> : IMqttIdStore
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
        [Flags]
        protected enum QoSState : byte
        {
            None = 0,
            QoS1 = 1 << 0,
            QoS2 = 1 << 1,
            QoS2PubRecAcked = 1 << 2,
            Dropped = 1 << 6,
            UncertainDead = 1 << 7,
            PacketAckedMask = QoS2PubRecAcked | UncertainDead,
            QoSMask = QoS1 | QoS2 | QoS2PubRecAcked
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
        readonly MqttConfigurationBase _config;
        TaskCompletionSource<object?>? _idFullTCS = null; //TODO: replace by non generic TCS in .NET 5
        TaskCompletionSource<object?>? _packetDroppedTCS = null;
        public MqttIdStore( int packetIdMaxValue, MqttConfigurationBase config )
        {
            _idStore = new( packetIdMaxValue, config.IdStoreStartCount );
            _stopwatch = config.StopwatchFactory.Create();
            _config = config;
        }

        public Task GetTaskResolvedOnPacketDropped()
        {
            lock( _idStore )
            {
                if( _packetDroppedTCS == null ) _packetDroppedTCS = new TaskCompletionSource<object?>();
                return _packetDroppedTCS.Task;
            }
        }
        public void ReleaseTCSPacketDropped()
        {
            lock(_idStore)
            {
                _packetDroppedTCS = null;
            }
        }

        [Pure]
        static bool WasPacketNeverAcked( QoSState state ) => (byte)(state & QoSState.PacketAckedMask) > 0;

        QoSState GetStateAndChecks( int packetId )
        {
            if( packetId > _idStore._count ) throw new ProtocolViolationException( "The sender acknowlodged a packet id that does not exist." );
            QoSState state = _idStore._entries[packetId - 1].Content._state;
            if( state == QoSState.None ) throw new ProtocolViolationException( "The sender acknowlodged a packet id that does not exist." );
            Debug.Assert( !state.HasFlag( QoSState.Dropped ) );
            return state;
        }

        void FreeId( IInputLogger? m, int packetId )
        {
            lock( _idStore )
            {
                _idStore.FreeId( m, packetId );
                _idFullTCS?.SetResult( null );
            }
        }

        void DropPreviousUnackedPacket( IInputLogger? m, ref IdStore<EntryContent>.Entry entry, int packetId )
        {
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
                        if( _packetDroppedTCS != null )
                        {
                            _packetDroppedTCS.SetResult( null );
                            _packetDroppedTCS = null;
                        }
                    }
                }
            }
        }

        protected ref T this[int packetId] => ref _idStore._entries[packetId - 1].Content.Storage;

        protected abstract ValueTask<IOutgoingPacket> DoStorePacket( IActivityMonitor? m, IOutgoingPacketWithId content );

        /// <summary> To be called when packet must be stored. Assign ID to the packet.</summary>
        /// <returns><see langword="null"/> when no packet id was available.</returns>
        public async ValueTask<Task<object?>> StoreMessageAsync( IActivityMonitor? m, IOutgoingPacketWithId packet, QualityOfService qos )
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
            packet.PacketId = packetId;
            entry._lastEmissionTime = _stopwatch.Elapsed;
            entry._attemptInTransitOrLost = 0;
            TaskCompletionSource<object?> tcs = new();
            entry._taskCompletionSource = tcs;
            packet = _config.StoreTransformer.PacketTransformerOnSave( packet );
            await DoStorePacket( m, packet );
            return tcs.Task;
        }

        public void OnPacketSent( IOutputLogger? m, int packetId )
        {
            lock( _idStore )
            {
                _idStore._entries[packetId - 1].Content._attemptInTransitOrLost++;
                _idStore._entries[packetId - 1].Content._lastEmissionTime = _stopwatch.Elapsed;
            }
        }

        protected abstract ValueTask RemovePacketData( IInputLogger? m, int packetId );

        public async ValueTask OnQos1AckAsync( IInputLogger? m, int packetId, object? result )
        {
            MqttIdStore<T>.QoSState state = GetStateAndChecks( packetId );
            Debug.Assert( (QualityOfService)((byte)state & (byte)QualityOfService.Mask) == QualityOfService.AtLeastOnce );

            bool wasNeverAcked = WasPacketNeverAcked( state );
            if( wasNeverAcked )
            {
                _idStore._entries[packetId - 1].Content._taskCompletionSource.SetResult( result ); // TODO: provide user a transaction window and remove packet when he is done..
                await RemovePacketData( m, packetId );
            }
            End();
            void End()
            {
                ref IdStore<EntryContent>.Entry entry = ref _idStore._entries[packetId - 1];
                DropPreviousUnackedPacket( m, ref entry, packetId );
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
        }

        public async ValueTask OnQos2AckStep1Async( IInputLogger? m, int packetId )
        {
            MqttIdStore<T>.QoSState state = GetStateAndChecks( packetId );
            Debug.Assert( (QualityOfService)((byte)state & (byte)QualityOfService.Mask) == QualityOfService.AtLeastOnce );

            bool wasNeverAcked = WasPacketNeverAcked( state );
            if( wasNeverAcked )
            {
                // We set to one because we don't care of the uncertain logic here. The next ack in the process will "clean" the pipe.
                _idStore._entries[packetId - 1].Content._attemptInTransitOrLost = 1;
                _idStore._entries[packetId - 1].Content._state |= QoSState.QoS2PubRecAcked;
                _idStore._entries[packetId - 1].Content._taskCompletionSource.SetResult( null ); // TODO: provide user a transaction window and remove packet when he is done..
                await RemovePacketData( m, packetId );
            }
            End();
            void End()
            {
                ref IdStore<EntryContent>.Entry entry = ref _idStore._entries[packetId - 1];
                DropPreviousUnackedPacket( m, ref entry, packetId );
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
        }

        public void OnQos2AckStep2( IInputLogger? m, int packetId )
        {
            MqttIdStore<T>.QoSState state = GetStateAndChecks( packetId );
            if( (state & QoSState.QoS2PubRecAcked) != QoSState.QoS2PubRecAcked )
            {
                throw new ProtocolViolationException( "PubRec not acked but we received PubRel" );
            }
            ref IdStore<EntryContent>.Entry entry = ref _idStore._entries[packetId - 1];
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

        protected abstract ValueTask<IOutgoingPacketWithId> RestorePacket( int packetId );

        async ValueTask<(IOutgoingPacketWithId?, TimeSpan)> RestorePacketInternal( int packetId )
            => (await RestorePacket( packetId ), TimeSpan.Zero);

        public ValueTask<(IOutgoingPacketWithId? outgoingPacket, TimeSpan timeUntilAnotherRetry)> GetPacketToResend()
        {
            // If there is no packet id allocated, there is no unacked packet id.
            if( _idStore.NoPacketAllocated ) return new ValueTask<(IOutgoingPacketWithId?, TimeSpan)>( (null, Timeout.InfiniteTimeSpan) );

            TimeSpan timeLimit = _stopwatch.Elapsed.Subtract( TimeSpan.FromMilliseconds( _config.WaitTimeoutMilliseconds ) );

            int currId = _idStore._tail;
            ref var curr = ref _idStore._entries[currId - 1];
            TimeSpan oldest = curr.Content._lastEmissionTime;
            while( currId != _idStore._oldestIdAllocated ) // We loop over all older packets.
            {
                currId = curr.PreviousId;
                curr = ref _idStore._entries[currId - 1];
                if( curr.Content._lastEmissionTime < timeLimit || curr.Content._state.HasFlag( QoSState.Dropped ) )
                {
                    curr.Content._state &= QoSState.QoSMask;
                    return RestorePacketInternal( currId ); // ValueTask<T> => ValueTask<T?> throw a warning.
                }
                if( curr.Content._lastEmissionTime > oldest ) oldest = curr.Content._lastEmissionTime;
            }
            return new ValueTask<(IOutgoingPacketWithId?, TimeSpan)>( (null, timeLimit - oldest) );
        }
    }
}
