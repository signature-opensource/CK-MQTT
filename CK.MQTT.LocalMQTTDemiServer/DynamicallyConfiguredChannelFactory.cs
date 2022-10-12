using CK.Core;
using CK.Observable.MQTT;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class DynamicallyConfiguredChannelFactory : IMQTTChannelFactory
    {
        MQTTDemiServerConfig? _currentConfig;
        readonly MultiChannelFactory _multiChannelFactory = new();
        public DynamicallyConfiguredChannelFactory( IOptionsMonitor<MQTTDemiServerConfig> config )
        {
            var cfg = config.CurrentValue;
            ApplyConfig( cfg );
            config.OnChange( ApplyConfig );
        }

        readonly Dictionary<int, IMQTTChannelFactory> _tcpFactories = new();
        IMQTTChannelFactory? _wsFactory;
        readonly HashSet<string> _prefixes = new();
#pragma warning disable VSTHRD100 // Avoid async void methods
        async void ApplyConfig( MQTTDemiServerConfig config ) // I don't see other way of async void sadly.
#pragma warning restore VSTHRD100 //
        {
            try
            {
                if( config == null )
                {
                    ActivityMonitor.StaticLogger.Error( "DefaultMQTTDemiServer.ChannelFactory: null config, ignoring." );
                    return;
                }
                config.ListenTo ??= new HashSet<string>( new[] { "tcp:1883" } );

                if( _currentConfig?.ListenTo == null || !config.ListenTo.SequenceEqual( _currentConfig!.ListenTo! ) )
                {
                    var m = new ActivityMonitor( $"{nameof( DynamicallyConfiguredChannelFactory )} logger." );
                    using( var grp = m.OpenTrace( "Applying new configuration." ) )
                    {
                        var factories = new List<IMQTTChannelFactory>();
                        bool success = true;
                        HashSet<int> ports = new();
                        HashSet<string> prefixes = new();
                        foreach( var item in config.ListenTo ) // Check loop
                        {
                            if( item.StartsWith( "tcp" ) )
                            {
                                int port = GetTcpConfigPort( m, item );
                                if( port == -1 ) success = false;
                                if( !ports.Add( port ) )
                                {
                                    m.Warn( "Duplicate port. Ignoring." );
                                }
                            }
                            else if( item.StartsWith( "ws" ) )
                            {
                                var prefix = GetWSConfig( m, item );
                                if( prefix == null ) success = false;
                                if( !prefixes.Add( prefix! ) )
                                {
                                    m.Warn( "Dupliate prefix, ignoring." );
                                }
                            }
                        }
                        if( !success )
                        {
                            grp.ConcludeWith( () => "Error while reading config, nothing was done." );
                            return;
                        }

                        foreach( var port in ports )
                        {
                            if( _tcpFactories.ContainsKey( port ) ) continue;

                            try
                            {
                                TcpChannelFactory factory = new TcpChannelFactory( port );
                                await _multiChannelFactory.AddFactoryAsync( factory );
                                _tcpFactories.TryAdd( port, factory );
                            }
                            catch( Exception e )
                            {
                                m.Error( $"Error while creating {nameof( TcpChannelFactory )}. Continuing applying configuration.", e );
                                success = false;
                            }
                        }
                        var unusedPorts = _tcpFactories.Where( s => !ports.Contains( s.Key ) );
                        foreach( var pair in unusedPorts )
                        {
                            _tcpFactories.Remove( pair.Key );
                            await _multiChannelFactory.RemoveFactoryAsync( m, pair.Value );
                            pair.Value.Dispose();
                        }

                        if( !prefixes.SetEquals( _prefixes ) )
                        {
                            _prefixes.Clear();
                            _prefixes.AddRange( prefixes );
                            if( _wsFactory is not null )
                            {
                                await _multiChannelFactory.RemoveFactoryAsync( m, _wsFactory );
                                _wsFactory.Dispose();
                            }
                            try
                            {
                                _wsFactory = new WebSocketChannelFactory( prefixes );
                                await _multiChannelFactory.AddFactoryAsync( _wsFactory );
                            }
                            catch( Exception e )
                            {
                                m.Error( $"Error while creating {nameof( WebSocketChannelFactory )}. Continuing applying configuration.", e );
                                success = false;
                            }
                        }

                        if( success )
                        {
                            grp.ConcludeWith( () => "Successfuly applied config." );
                        }
                        else
                        {
                            grp.ConcludeWith( () => "Configuration has been partially(or not at all) applied due to some error." );
                        }
                        return;
                    }
                }

                _currentConfig = config;
            }
            catch( Exception e )
            {
                ActivityMonitor.StaticLogger.Error( "Error while applying configuration.", e );
            }

        }
        static int GetTcpConfigPort( IActivityMonitor m, string item )
        {
            var splitted = item.Split( ":" );
            if( splitted.Length > 2 )
            {
                m.Error( $"Error multiple ':' were given to ListenTo:'{item}', the format is tcp:{{portNumber}}" );
                return -1;
            }
            if( splitted.Length == 1 )
            {
                m.Info( $"ListenTo:'{item}', No port was specified, defaulting to 1883." );
                return 1883;
            }
            else
            {
                if( int.TryParse( splitted[1], out int port ) ) return port;
                m.Error( $"{splitted[1]} in ListenTo:'{item}' was not parsed as int. It must be a valid int" );
                return -1;
            }
        }
        static string? GetWSConfig( IActivityMonitor m, string item ) => item.Substring( 3 );

        public ValueTask<(IMQTTChannel channel, string connectionInfo)> CreateAsync( CancellationToken cancellationToken )
            => _multiChannelFactory.CreateAsync( cancellationToken );

        public void Dispose() => _multiChannelFactory.Dispose();
    }
}
