using CK.Core;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.MQTT.Server
{
    public class DynamicallyConfiguredAuthenticationProtocolHandlerFactory : IAuthenticationProtocolHandlerFactory
    {
        //readonly IOptionsMonitor<MQTTDemiServerConfig> _config;
        //MQTTDemiServerConfig? _currentConfig;
        //IDisposable _disposable;
        public DynamicallyConfiguredAuthenticationProtocolHandlerFactory( IOptionsMonitor<MQTTDemiServerConfig> config )
        {
            //if( config.CurrentValue.AuthMode != "insecure" ) throw new NotSupportedException( "Only insecure is supported right now." );
            //_config = config;
            //var cfg = config.CurrentValue;
            //if( cfg.AuthMode != "insecure" ) throw new InvalidDataException( "Invalid AuthMode, only insecure is supported." );
            //ApplyConfig( cfg );
            //_disposable = _config.OnChange( ApplyConfig );
        }
        public ValueTask<IAuthenticationProtocolHandler?> ChallengeIncomingConnectionAsync( string connectionInfo,
                                                                                      CancellationToken cancellationToken )
            => new( new InsecureAuthHandler() );

        //void ApplyConfig( MQTTDemiServerConfig config )
        //{
        //    if( config == null )
        //    {
        //        ActivityMonitor.StaticLogger.Error( "DefaultMQTTDemiServer.ProtocolHandlerFactory: null config, ignoring." );
        //        return;
        //    }
        //    if( config.AuthMode != "insecure" )
        //    {
        //        ActivityMonitor.StaticLogger.Error( "DefaultMQTTDemiServer.ProtocolHandlerFactory: Only insecure auth mode is supported right now, ignoring new config..." );
        //        return;
        //    }
        //    _currentConfig = config;
        //}

        public void Dispose()
        {
            //_disposable.Dispose();
        }
    }
}
