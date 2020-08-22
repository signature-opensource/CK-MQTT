using CK.Core;
using System;
using System.IO.Pipelines;

namespace CK.MQTT
{
    /// <summary>
    /// Configuration of a <see cref="IMqttClient"/>.
    /// </summary>
    public class MqttConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttConfiguration" /> class.
        /// </summary>
        /// <param name="connectionString">The connection string that will be used by the <see cref="IMqttChannelFactory"/>.</param>
        /// <param name="keepAliveSecs">If the client didn't sent a packet in the given amount of time, it will send a PingRequest packet.
        /// <br/>0 to disable the KeepAlive mechanism.</param>
        /// <param name="waitTimeoutSecs">Time before the client try to resend a packet.</param>
        /// <param name="attemptCountBeforeGivingUpPacket"></param>
        /// <param name="inputLogger">The logger to use to log the activities while processing the incoming data.</param>
        /// <param name="outputLogger">The logger to use to log the activities while processing the outgoing data.</param>
        /// <param name="channelFactory">Factory that create a channel, used to communicated with the broker.</param>
        /// <param name="storeFactory">Factory that create a store, used to store packets.</param>
        /// <param name="storeTransformer">The store transformer allow to modify packet while they are stored, or sent.</param>
        /// <param name="readerOptions">Options to configurate the <see cref="PipeReader"/>.</param>
        /// <param name="writerOptions">Options to configurate the <see cref="PipeWriter"/>.</param>
        public MqttConfiguration(
            string connectionString,
            ushort keepAliveSecs = 0,
            int waitTimeoutSecs = -1,
            ushort attemptCountBeforeGivingUpPacket = 50,
            IMqttChannelFactory? channelFactory = null,
            IStoreFactory? storeFactory = null,
            IStoreTransformer? storeTransformer = null,
            StreamPipeReaderOptions? readerOptions = null,
            StreamPipeWriterOptions? writerOptions = null
        )
        {
            ConnectionString = connectionString;
            KeepAliveSecs = keepAliveSecs;
            WaitTimeoutMs = waitTimeoutSecs;
            AttemptCountBeforeGivingUpPacket = attemptCountBeforeGivingUpPacket;
            ChannelFactory = channelFactory ?? new TcpChannelFactory();
            StoreFactory = storeFactory ?? new MemoryStoreFactory();
            StoreTransformer = storeTransformer ?? DefaultStoreTransformer.Default;
            ReaderOptions = readerOptions;
            WriterOptions = writerOptions;
        }

        public string ConnectionString { get; }

        /// <summary>
        /// Seconds to wait for the MQTT Keep Alive mechanism
        /// until a Ping packet is sent to maintain the connection alive
        /// Default value is 0 seconds, which means Keep Alive disabled
        /// </summary>
        public ushort KeepAliveSecs { get; }

        /// <summary>
        /// Seconds to wait for an incoming required message until the operation timeouts
        /// This value is generally used to wait for Server or Client acknowledgements
        /// Default value is 5 seconds
        /// </summary>
		public int WaitTimeoutMs { get; }
        //0 to disable
        public ushort AttemptCountBeforeGivingUpPacket { get; }
        public IInputLogger? InputLogger { get; set; }
        public IOutputLogger? OutputLogger { get; set; }
        public IMqttChannelFactory ChannelFactory { get; }
        public IStoreFactory StoreFactory { get; }
        public IStoreTransformer StoreTransformer { get; }
        public StreamPipeReaderOptions? ReaderOptions { get; }

        public StreamPipeWriterOptions? WriterOptions { get; }

        public int ChannelsPacketCount { get; } = 32;
    }
}
