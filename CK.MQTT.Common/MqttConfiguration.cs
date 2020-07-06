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
            IStoreTransformer? storeTransformer = null,
            StreamPipeReaderOptions? readerOptions = null,
            StreamPipeWriterOptions? writerOptions = null
        )
        {
            ConnectionString = connectionString;
            KeepAliveSecs = keepAliveSecs;
            WaitTimeoutMs = waitTimeoutSecs;
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
        public IStoreTransformer StoreTransformer { get; }
        public StreamPipeReaderOptions? ReaderOptions { get; }

        public StreamPipeWriterOptions? WriterOptions { get; }

        public int ChannelsPacketCount { get; } = 32;

    }
}
