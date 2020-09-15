using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// This class plays with an input and an output pump.
    /// </summary>
    public abstract class Pumppeteer<TInPump,TOutPump>
        where TInPump : PumpBase
        where TOutPump : PumpBase
    {
        TInPump? _input;
        TOutPump? _output;

        /// <summary>
        /// Gets the input pump.
        /// Null when <see cref="OpenPumps(TInPump, TOutPump)"/> has not been called or <see cref="ClosePumps"/> has been called last.
        /// </summary>
        protected TInPump? InputPump => _input;

        /// <summary>
        /// Gets the output pump.
        /// Null when <see cref="OpenPumps(TInPump, TOutPump)"/> has not been called or <see cref="ClosePumps"/> has been called last.
        /// </summary>
        protected TOutPump? OutputPump => _output;

        protected void OpenPumps( TInPump input, TOutPump output )
        {
            if( _input != null ) throw new InvalidOperationException();
            if( input == null ) throw new ArgumentNullException( nameof( input ) );
            if( output == null ) throw new ArgumentNullException( nameof( output ) );
            _input = input;
            _output = output;
        }

        protected Task ClosePumps()
        {

        }

    }
}
