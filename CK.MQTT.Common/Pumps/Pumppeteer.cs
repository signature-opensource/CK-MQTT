using CK.Core;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
#nullable enable

namespace CK.MQTT
{

    public abstract class Pumppeteer<T> : PumppeteerBase where T : Pumppeteer<T>.StateHolder
    {
        /// <summary>
        /// State that is set to <see langword="null"/> when the <see cref="Pumppeteer{T}"/> is closed.
        /// </summary>
        public class StateHolder
        {
            /// <summary>
            /// The input pump.
            /// <see langword="null"/> when <see cref="OpenPumps"/> has not been called or <see cref="ClosePumps"/> has been called last.
            /// </summary>
            public readonly InputPump Input;

            /// <summary>
            /// Gets the output pump.
            /// <see langword="null"/> when <see cref="OpenPumps"/> has not been called or <see cref="ClosePumps"/> has been called last.
            /// </summary>
            public readonly OutputPump OutputPump;
            public StateHolder( InputPump input, OutputPump output ) => (Input, OutputPump) = (input, output);
        }

        /// <summary>
        /// Initializes a specialized <see cref="PumppeteerBase"/>.
        /// </summary>
        /// <param name="configuration">The MQTT basic configuration.</param>
        protected Pumppeteer( MqttConfigurationBase configuration ) : base( configuration ) { }

        T? _state;

        protected T? State => _state;

        /// <summary>
        /// Initializes the pumps: this "pumpeeter" becomes open.
        /// There is only 2 ways to close this pumpeeter from now on:
        /// <list type="bullet">
        ///     <item>From the outside by calling the <see cref="DisconnectAsync"/> public method.</item>
        ///     <item>From one of the two pump by calling the <see cref="PumpBase.DisconnectAsync(DisconnectedReason)"/>.</item>
        /// </list>
        /// </summary>
        protected void OpenPumps( IActivityMonitor? m, T newState )
        {
            using( m?.OpenInfo( "Opening pumps." ) )
            {
                Open();
                _state = newState;
            }
        }

        protected override async ValueTask OnClosed( DisconnectedReason reason )
        {
            StateHolder state = _state!;
            await Task.WhenAll( state.Input.CloseAsync(), state.OutputPump.CloseAsync() );
            await base.OnClosed( reason );
            _state = null;
        }




    }
}
