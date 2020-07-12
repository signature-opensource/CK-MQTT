using CK.Core;
using System;
using System.IO.Pipelines;

namespace CK.MQTT
{
    /// <summary>
    /// General configuration used across the protocol implementation
    /// </summary>
    public class MqttConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttConfiguration" /> class 
        /// </summary>
		public MqttConfiguration(
            string connectionString,
            ushort keepAliveSecs = 0,
            int waitTimeoutSecs = -1,
            bool waitForConnectionBeforeReSendingMessages = false,
            IMqttLogger? inputLogger = null,
            IMqttLogger? outputLogger = null,
            IMqttLogger? keepAliveLogger = null,
            IMqttChannelFactory? channelFactory = null,
            IStoreFactory? storeFactory = null,
            IStoreTransformer? storeTransformer = null,
            StreamPipeReaderOptions? readerOptions = null,
            StreamPipeWriterOptions? writerOptions = null
        )
        {
            if( keepAliveSecs > 0 && keepAliveLogger is null ) throw new ArgumentNullException( $"{nameof( keepAliveLogger )} cannot be null when keepAlive is enabled." );
            ConnectionString = connectionString;
            KeepAliveSecs = keepAliveSecs;
            WaitTimeoutMs = waitTimeoutSecs;
            WaitForConnectionBeforeReSendingMessages = waitForConnectionBeforeReSendingMessages;
            InputLogger = inputLogger ?? new MqttActivityMonitor( new ActivityMonitor() );
            OutputLogger = outputLogger ?? new MqttActivityMonitor( new ActivityMonitor() );
            KeepAliveLogger = keepAliveLogger;
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
        public bool WaitForConnectionBeforeReSendingMessages { get; }
        public IMqttLogger InputLogger { get; }
        public IMqttLogger OutputLogger { get; }
        public IMqttLogger? KeepAliveLogger { get; }
        public IMqttChannelFactory ChannelFactory { get; }
        public IStoreFactory StoreFactory { get; }
        public IStoreTransformer StoreTransformer { get; }
        public StreamPipeReaderOptions? ReaderOptions { get; }

        public StreamPipeWriterOptions? WriterOptions { get; }

        public int ChannelsPacketCount { get; } = 32;
    }
}
