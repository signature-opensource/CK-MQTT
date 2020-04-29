using CK.Core;
using CK.MQTT.Common.Packets;
using System;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Channels
{
    public static class MqttChannelExtension
    {
        /// <summary>
        /// Helper that send a message in a channel and return a response to it.
        /// The response is the first message .
        /// If the predicate is null, 
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <param name="this"></param>
        /// <param name="m"></param>
        /// <param name="dataToSend"></param>
        /// <param name="responsePredicate"></param>
        /// <param name="receiveTimeoutMilliseconds"></param>
        /// <returns></returns>
        public static async Task<Task<TReceive?>> SendAndWaitResponse<TBase, TReceive>(
            this IMqttChannel<TBase> @this,
            IActivityMonitor m,
            TBase dataToSend,
            Func<TReceive, bool>? responsePredicate,
            int receiveTimeoutMilliseconds = -1
        )
            where TBase : class
            where TReceive : class, TBase
        {
            //There may be a race condition where the server answer immediatly, so we must start to listen before we sent the packet.
            Task<TReceive?> responseListen = @this.WaitMessageReceivedAsync( responsePredicate, receiveTimeoutMilliseconds );
            await @this.SendAsync( m, dataToSend, default );
            return responseListen;
        }

        public static async Task<Task<TReceive?>> SendAndWaitResponseAndLog<TBase, TReceive>(
            this IMqttChannel<TBase> @this,
            IActivityMonitor m,
            TBase packetToSend,
            Func<TReceive, bool>? responsePredicate,
            int receiveTimeoutMilliseconds = -1
        )
            where TBase : class
            where TReceive : class, TBase
        {
            var grp = m.OpenTrace( $"Sending packet and expecting response..." );
            Task<TReceive?> tsk = await SendAndWaitResponse( @this, m, packetToSend, responsePredicate, receiveTimeoutMilliseconds );
            return tsk.ContinueWith( s =>
            {
                string concludeText = s.Result == null ? "Timeout while waiting the response." : $"Received response in the given time.";
                grp.ConcludeWith( () => concludeText );
                return s.Result;
            } );
        }

        public static async Task<TReceive> SendAndWaitResponseWithRetries<TBase, TReceive, TSended>( this IMqttChannel<IPacket> @this,
            IActivityMonitor m,
            TSended packetToSend,
            Func<TReceive, bool>? responsePredicate,
            int timeoutUntilRetryMillisecond,
            Func<TSended, TSended>? transformOnRetry = null )
            where TBase : class, IPacket
            where TReceive : class, TBase
            where TSended : TBase
        {
            TReceive? output = await await SendAndWaitResponseAndLog( @this, m, packetToSend, responsePredicate, timeoutUntilRetryMillisecond );
            while( output == null )
            {
                if( transformOnRetry != null ) packetToSend = transformOnRetry( packetToSend );
                output = await await SendAndWaitResponseAndLog( @this, m, packetToSend, responsePredicate, timeoutUntilRetryMillisecond );
            };
            return output;
        }
    }
}
