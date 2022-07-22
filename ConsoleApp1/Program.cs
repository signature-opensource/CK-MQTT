using CK.Core;
using CK.MQTT.Server;
using CK.MQTT.Server.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{

    public class Program
    {
        public static Task Main( string[] args )
        {
            var m = new ActivityMonitor( "App Startup" );

            var host = new HostBuilder()
                .UseContentRoot( Directory.GetCurrentDirectory() )
                .ConfigureHostConfiguration( config =>
                {
                    config.AddEnvironmentVariables( prefix: "DOTNET_" );
                    config.AddCommandLine( args );
                } )
                .ConfigureAppConfiguration( ( hostingContext, config ) =>
                {
                    config.AddJsonFile( "appsettings.json", optional: true, reloadOnChange: true )
                          .AddJsonFile( $"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true );

                    // Configuration coming from the environment variables are considered safer than appsettings configuration.
                    // We add them after the appsettings.
                    config.AddEnvironmentVariables();

                    // Finally comes the configuration values from the command line: these configurations override
                    // any previous ones.
                    config.AddCommandLine( args );
                } )
                .UseCKMonitoring()
                .ConfigureServices( ( context, s ) =>
                {
                    s.AddStObjMap( m, Assembly.GetExecutingAssembly() )
                    .AddHostedService<MyMQTTService>();
                } ).Build();

            return host.RunAsync();
        }

    }

    public class MyMQTTService : IHostedService
    {
        readonly DefaultMQTTDemiServer _server;

        public MyMQTTService( DefaultMQTTDemiServer server )
        {
            _server = server;
            _server.OnNewClient.Sync += OnNewClient_Sync;
        }

        public Task StartAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        public Task StopAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        private void OnNewClient_Sync( IActivityMonitor monitor, MqttServerAgent e )
        {
            monitor.Info( $"Hello client {e.ClientId}" );
        }
    }
}
