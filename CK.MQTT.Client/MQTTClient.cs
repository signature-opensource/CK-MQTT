using CK.MQTT.Client.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT.Client
{
    public static class MQTTClient
    {
        public class MQTTClientBuilder
        {
            readonly bool _autoReconnect;
            readonly MQTT3ClientConfiguration _config;
            public MQTTClientBuilder()
            {
                _config = new();
                _autoReconnect = false;
            }

            MQTTClientBuilder( MQTT3ClientConfiguration config, bool autoReconnect )
            {
                _config = config;
                _autoReconnect = autoReconnect;
            }

            public MQTTClientBuilder WithConfig( MQTT3ClientConfiguration config )
                => new MQTTClientBuilder( config, _autoReconnect );

            public MQTTClientBuilder WithConfig( Action<MQTT3ClientConfiguration> configure )
            {
                configure( _config );
                return this;
            }

            public MQTTClientBuilder WithAutoReconnect()
                => new MQTTClientBuilder( _config, true );

            public IMQTT3Client Build()
            {
                var messageWorker = new MessageWorker();

                var sink = new DefaultClientMessageSink( messageWorker.MessageWriter );
                var channel = ChannelFromConnectionString( _config.ConnectionString );
                var client = new LowLevelMQTTClient( ProtocolConfiguration.MQTT3, _config, sink, channel );
                if( _autoReconnect )
                {
                    messageWorker.Middlewares.Add( new HandleAutoReconnect(
                        _config.TimeUtilities,
                        client,
                        messageWorker.MessageWriter,
                        AutoReconnect
                    ) );
                }
                var agent = new MQTTClientAgent( client, messageWorker );
                return agent;
            }
        }

        static TimeSpan AutoReconnect( TimeSpan timeSpan )
        {
            if( timeSpan.TotalSeconds < 5 ) return TimeSpan.FromSeconds( 5 ) - timeSpan;
            return TimeSpan.Zero;
        }

        static IMQTTChannel ChannelFromConnectionString( string connectionString )
        {
            var split = connectionString.Split();
            string protocol;
            string hostname;
            int port;
            if( split.Length > 3 || split.Length == 0 ) throw new ArgumentException( null, nameof( connectionString ) );
            else if( split.Length == 3 )
            {
                protocol = split[0];
                hostname = split[1];
                port = int.Parse( split[2] );
            }
            else if( split.Length == 2 )
            {
                if( int.TryParse( split[1], out port ) )
                {
                    protocol = "tcp";
                    hostname = split[0];
                }
                else
                {
                    protocol = split[0];
                    hostname = split[1];
                    port = 1883;
                }
            }
            else if( split.Length == 1 )
            {
                protocol = "tcp";
                hostname = split[0];
                port = 1883;
            }
            else
            {
                throw new InvalidOperationException( "Unreachable code path." );
            }
            if( protocol == "tcp" )
            {
                return new TcpChannel( hostname, port );
            }
            if( protocol == "ws" )
            {
                return new WebSocketChannel( new Uri( hostname ) );
            }
            throw new NotSupportedException( $"Protocol {protocol} is not supported." );
        }
    }
}
