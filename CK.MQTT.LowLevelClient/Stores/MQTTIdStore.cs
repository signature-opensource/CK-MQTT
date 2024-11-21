using CK.MQTT.Client;
using CK.MQTT.Common.Stores;
using CK.MQTT.Packets;
using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Stores;

public abstract class MQTTIdStore<T> : ILocalPacketStore
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
    //   This second packet is like the Schroedinger's cat.
    //   We can directly observe that the packet is alive, when we receive it.
    //   But we can't directly observe that the packet is dead.
    //   But we can observe indirectly it's death, because MQTT specify that:
    //      - The underlying network MUST respect the packets orders.
    //      - The receiver MUST respond in the same order it received the message.
    //   If we observe an Ack for any message that was sent after the last retry,
    //      we indirectly observed the packet death, because we should had received it before.
    //   
    //   So, contrary to what the MQTT specs says, in this case we MUST NOT free the ID right after the ack reception.
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
        QosMask = QoS1 | QoS2,
        QoSStateMask = QosMask | QoS2PubRecAcked,
    }
    protected struct EntryContent
    {
        internal TimeSpan _lastEmissionTime;
        public T Storage;
        internal ulong _attemptInTransitOrLost;
        internal QoSState _state;
        internal TaskCompletionSource<object?> _taskCompletionSource;

        public override bool Equals( object? obj ) => throw new NotSupportedException();

        public override int GetHashCode() => throw new NotSupportedException();

        public static bool operator ==( MQTTIdStore<T>.EntryContent left, MQTTIdStore<T>.EntryContent right ) => left.Equals( right );

        public static bool operator !=( MQTTIdStore<T>.EntryContent left, MQTTIdStore<T>.EntryContent right ) => !(left == right);
    }

    readonly IdStore<EntryContent> _idStore;
    readonly IStopwatch _stopwatch;
    protected readonly MQTT3ConfigurationBase Config;
    TaskCompletionSource? _idFullTCS = null;
    int _droppedCount;
    protected MQTTIdStore( ushort packetIdMaxValue, MQTT3ConfigurationBase config )
    {
        _idStore = new( packetIdMaxValue, config.IdStoreStartCount );
        _stopwatch = config.TimeUtilities.CreateStopwatch();
        _stopwatch.Start();
        Config = config;
    }

    /// <summary>
    /// Call it only with a lock.
    /// </summary>
    protected ref IdStoreEntry<EntryContent> this[uint index] => ref _idStore._entries[index];

    /// <summary>
    /// Require lock.
    /// </summary>
    /// <param name="packetId"></param>
    /// <returns></returns>
    QoSState GetStateAndChecks( ushort packetId )
    {
        if( packetId > _idStore._entries.Length ) throw new ProtocolViolationException( "The sender acknowledged a packet id that does not exist." );
        QoSState state = _idStore._entries[packetId].Content._state;
        if( state == QoSState.None ) throw new ProtocolViolationException( "The sender acknowledged a packet id that does not exist." );
        Debug.Assert( !state.HasFlag( QoSState.Dropped ) );
        return state;
    }
    public bool IsRevivedSession { get; set; }

    /// <summary>
    /// Require lock.
    /// </summary>
    /// <param name="m"></param>
    /// <param name="packetId"></param>
    void FreeId( ushort packetId )
    {
        lock( _idStore )
        {
            _idStore.FreeId( packetId );
            _idFullTCS?.SetResult();
        }
    }

    /// <summary>
    /// Require lock.
    /// </summary>
    /// <param name="m"></param>
    /// <param name="entry"></param>
    /// <param name="packetId"></param>
    bool DropPreviousUnackedPacket( IMQTT3Sink sink, ref IdStoreEntry<EntryContent> entry, ushort packetId )
    {
        bool dropped = false;
        var currId = packetId;
        lock( _idStore )
        {
            ref var curr = ref _idStore._entries[currId];
            while( currId != _idStore._head ) // We loop over all older packets.
            {
                if( curr.Content._lastEmissionTime < entry.Content._lastEmissionTime )
                {
                    // The last retry of this packet occurred before the current packet was sent.
                    // We can assert that this ack packet was dropped.
                    if( (curr.Content._state & QoSState.UncertainDead) == QoSState.UncertainDead )
                    {
                        // If the packet was in an uncertain state, it mean the ack logic ran.
                        // But we could not knew if there was another ack in the pipe or not.
                        // Now we know that no such ack is in transit.
                        // So we can simply free it now.
                        FreeId( currId );
                    }
                    else
                    {
                        sink.OnPacketResent( packetId, curr.Content._attemptInTransitOrLost, true );
                        curr.Content._state |= QoSState.Dropped; // We mark the packet as dropped so it can be resent immediately.
                        _droppedCount++;
                        dropped = true;
                    }
                }

                currId = curr.PreviousId;
                curr = ref _idStore._entries[currId];
            }
        }
        return dropped;
    }

    protected abstract ValueTask DoResetAsync( ArrayStartingAt1<IdStoreEntry<EntryContent>> entries );

    /// <summary>
    /// Not thread safe.
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    public async ValueTask ResetAsync()
    {
        await DoResetAsync( _idStore._entries );
        _idStore.Reset();
    }

    protected abstract ValueTask<IOutgoingPacket> DoStorePacketAsync( IOutgoingPacket packet );

    /// <summary>
    /// Used when we have to discard QoS packet, but have to store that we will send a PUBREL.
    /// The overwrite will always be smaller that the original message.
    /// </summary>
    protected abstract ValueTask<IOutgoingPacket> OverwriteMessageAsync( IOutgoingPacket packet ); //TODO MQTT5: check if the overwrite can be bigger in size.

    /// <summary> To be called when packet must be stored. Assign ID to the packet.</summary>
    /// <returns><see langword="null"/> when no packet id was available.</returns>
    public async ValueTask<(Task<object?> ackTask, IOutgoingPacket packetToSend)> StoreMessageAsync( IOutgoingPacket packet, QualityOfService qos )
    {
        Debug.Assert( qos != QualityOfService.AtMostOnce );
        EntryContent entry = new()
        {
            _lastEmissionTime = _stopwatch.Elapsed,
            _attemptInTransitOrLost = 0,
            _state = (QoSState)(byte)qos,
            _taskCompletionSource = new()
        };

        ushort packetId;
        bool res = false;
        lock( _idStore )
        {
            // Lock because id store is not thread safe.
            res = _idStore.CreateNewEntry( entry, out packetId );
        }
        // We don't need to lock more than that, packet is not sent yet so we wont receive an ack for this ID.

        while( !res )
        {
            lock( _idStore )
            {
                res = _idStore.CreateNewEntry( entry, out packetId );
                _idFullTCS = new();
            }
            // Asynchronously wait that a new packet id is available.
            await _idFullTCS.Task.WaitAsync( TimeSpan.FromMilliseconds( Config.StoreFullWaitTimeoutMs ) );
            if( !_idFullTCS.Task.IsCompleted )
            {
                throw new OperationCanceledException( $"The store is full, and the configured timeout has been reached is {Config.StoreFullWaitTimeoutMs}." );
            }
        }

        packet.PacketId = packetId;
        packet = Config.StoreTransformer.PacketTransformerOnSave( packet );
        IOutgoingPacket packetToReturn = await DoStorePacketAsync( packet );
        return (entry._taskCompletionSource.Task, packetToReturn);
    }

    public void OnPacketSent( ushort packetId )
    {
        lock( _idStore )
        {
            if( _idStore._entries[packetId].Content._state.HasFlag( QoSState.Dropped ) )
            {
                _droppedCount--;
            }
            _idStore._entries[packetId].Content._attemptInTransitOrLost++;
            _idStore._entries[packetId].Content._state &= QoSState.QoSStateMask;
            _idStore._entries[packetId].Content._lastEmissionTime = _stopwatch.Elapsed;
        }
    }

    protected abstract void RemovePacketData( ref T storage );

    public bool OnQos1Ack( IMQTT3Sink sink, ushort packetId, object? result )
    {
        lock( _idStore )
        {
            MQTTIdStore<T>.QoSState state = GetStateAndChecks( packetId );
            Debug.Assert( (QualityOfService)((byte)state & (byte)QualityOfService.Mask) == QualityOfService.AtLeastOnce );

            // If it was already acked, the packet would be marked as "UncertainDed".
            bool wasNeverAcked = (state & QoSState.UncertainDead) != QoSState.UncertainDead;
            if( wasNeverAcked )
            {
                ref MQTTIdStore<T>.EntryContent content = ref _idStore._entries[packetId].Content;
                content._taskCompletionSource.SetResult( result ); // TODO: provide user a transaction window and remove packet when he is done..
                RemovePacketData( ref content.Storage );
            }
            return End();
            bool End()
            {
                ref IdStoreEntry<EntryContent> entry = ref _idStore._entries[packetId];
                bool res = DropPreviousUnackedPacket( sink, ref entry, packetId );
                if( entry.Content._attemptInTransitOrLost > 1 )
                {
                    entry.Content._attemptInTransitOrLost--;
                    entry.Content._state |= QoSState.UncertainDead;
                }
                else
                {
                    FreeId( packetId );
                }
                return res;
            }
        }
    }

    public async ValueTask<IOutgoingPacket> OnQos2AckStep1Async( ushort packetId )
    {
        lock( _idStore )
        {

            MQTTIdStore<T>.QoSState state = GetStateAndChecks( packetId );
            Debug.Assert( (QualityOfService)((byte)state & (byte)QualityOfService.Mask) == QualityOfService.ExactlyOnce );

            bool wasNeverAcked = (state & QoSState.QoS2PubRecAcked) != QoSState.QoS2PubRecAcked;
            if( !wasNeverAcked ) return LifecyclePacketV3.Pubrel( packetId );
            DoRemovePacket();
            void DoRemovePacket()
            {
                ref var content = ref _idStore._entries[packetId].Content;
                // We don't have to keep count of the previous retries. The next ack in the process will allow us to know that there was no more packet in the pipe.
                content._attemptInTransitOrLost = 0;
                content._state |= QoSState.QoS2PubRecAcked;
                content._taskCompletionSource.SetResult( null ); // TODO: provide user a transaction window and remove packet when he is done.
            }
        }
        return await OverwriteMessageAsync( LifecyclePacketV3.Pubrel( packetId ) );
    }

    public void OnQos2AckStep2( ushort packetId )
    {
        lock( _idStore )
        {
            MQTTIdStore<T>.QoSState state = GetStateAndChecks( packetId );
            if( (state & QoSState.QoS2PubRecAcked) != QoSState.QoS2PubRecAcked )
            {
                throw new ProtocolViolationException( "PubRec not acked but we received PubRel" );
            }
            ref IdStoreEntry<EntryContent> entry = ref _idStore._entries[packetId];
            entry.Content._attemptInTransitOrLost--;
            if( entry.Content._attemptInTransitOrLost > 0 )
            {
                entry.Content._state = QoSState.UncertainDead | QoSState.QoS2PubRecAcked;
            }
            else
            {
                FreeId( packetId );
            }
        }
    }

    protected abstract ValueTask<IOutgoingPacket> RestorePacketAsync( ushort packetId );

    async ValueTask<(IOutgoingPacket?, TimeSpan)> RestorePacketInternalAsync( ushort packetId )
        => (await RestorePacketAsync( packetId ), TimeSpan.Zero);

    public ValueTask<(IOutgoingPacket? outgoingPacket, TimeSpan timeUntilAnotherRetry)> GetPacketToResendAsync()
    {
        lock( _idStore )
        {
            // We track the current time inside the lock, so there are no entry younger than the current time.
            TimeSpan currentTime = _stopwatch.Elapsed;
            TimeSpan timeOut = TimeSpan.FromMilliseconds( Config.WaitTimeoutMilliseconds );
            // If there is no packet id allocated, there is no unacked packet id.
            if( _idStore.NoPacketAllocated ) return new ValueTask<(IOutgoingPacket?, TimeSpan)>( (null, Timeout.InfiniteTimeSpan) );
            var currId = _idStore._head;
            var nextId = currId;
            TimeSpan oldest = TimeSpan.MaxValue;
            IdStoreEntry<EntryContent> oldestEntry = default;

            do
            {
                ref var curr = ref _idStore._entries[nextId];
                currId = nextId;
                if( curr.Content._state.HasFlag( QoSState.UncertainDead ) ) continue;
                // If there is a packet that reached the peremption time, or is marked as dropped.
                if( curr.Content._lastEmissionTime + timeOut <= currentTime
                    || curr.Content._state.HasFlag( QoSState.Dropped ) )
                {
                    return RestorePacketInternalAsync( currId );
                }
                if( curr.Content._lastEmissionTime < oldest )
                {
                    oldestEntry = curr;
                    oldest = curr.Content._lastEmissionTime;
                }
                nextId = curr.NextId;
            } while( currId != _idStore._newestIdAllocated ); // We loop over all older packets.
            TimeSpan timeUntilAnotherRetry = oldest + timeOut - currentTime;

            Debug.Assert( timeUntilAnotherRetry.TotalMilliseconds > 0 );
            return new ValueTask<(IOutgoingPacket?, TimeSpan)>( (null, timeUntilAnotherRetry) );
        }
    }

    public void CancelAllAckTask()
    {
        lock( _idStore )
        {
            for( int i = 1; i < _idStore._entries.Length + 1; i++ )
            {
                _idStore._entries[i].Content._taskCompletionSource?.TrySetCanceled();
            }
        }
    }

    public abstract void Dispose();
}
