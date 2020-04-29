using CK.Core;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT
{

    /// <summary>
    /// Event handler that can be combined into a <see cref="SequentialEventHandlerSender{T}"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An object that contains no event data (<see cref="EventArgs.Empty"/> should be used).</param>
    public delegate void SequentialEventHandler<TSender,TArg>( IActivityMonitor monitor, TSender sender, TArg e );

    /// <summary>
    /// Implements a host for <see cref="SequentialEventHandler{TSender,TArg}"/> delegates.
    /// </summary>
    public class SequentialEventHandlerSender<TSender, TArg>
    {
        object? _handler;

        /// <summary>
        /// Gets whether at least one handler is registered.
        /// </summary>
        public bool HasHandlers => _handler != null;

        /// <summary>
        /// Adds a handler. This is an atomic (thread safe) operation.
        /// </summary>
        /// <param name="h">Non null handler.</param>
        public SequentialEventHandlerSender<TSender, TArg> Add( SequentialEventHandler<TSender, TArg> handler )
        {
            if( handler == null ) throw new ArgumentNullException( nameof( handler ) );
            Util.InterlockedSet( ref _handler, h =>
            {
                if( h == null ) return handler;
                if( h is SequentialEventHandler<TSender, TArg> a ) return new SequentialEventHandler<TSender, TArg>[] { a, handler };
                var ah = (SequentialEventHandler<TSender, TArg>[])h;
                int len = ah.Length;
                Array.Resize( ref ah, len + 1 );
                ah[len] = handler;
                return ah;
            } );
            return this;
        }

        /// <summary>
        /// Removes a handler if it exists. This is an atomic (thread safe) operation.
        /// </summary>
        /// <param name="h">The handler to remove. Cannot be null.</param>
        public SequentialEventHandlerSender<TSender, TArg> Remove( SequentialEventHandler<TSender, TArg> handler )
        {
            if( handler == null ) throw new ArgumentNullException( nameof( handler ) );
            Util.InterlockedSet( ref _handler, h =>
            {
                if( h == null ) return null;
                if( h is SequentialEventHandler<TSender, TArg> a ) return a == handler ? null : h;
                var current = (SequentialEventHandler<TSender, TArg>[])h;
                int idx = Array.IndexOf( current, handler );
                if( idx < 0 ) return current;
                Debug.Assert( current.Length > 1 );
                var ah = new SequentialEventHandler<TSender, TArg>[current.Length - 1];
                Array.Copy( current, 0, ah, 0, idx );
                Array.Copy( current, idx + 1, ah, idx, ah.Length - idx );
                return ah;
            } );
            return this;
        }

        /// <summary>
        /// Relays to <see cref="Add"/>.
        /// </summary>
        /// <param name="eventHost">The host.</param>
        /// <param name="handler">The non null handler to add.</param>
        /// <returns>The host.</returns>
        public static SequentialEventHandlerSender<TSender, TArg> operator +( SequentialEventHandlerSender<TSender, TArg> eventHost, SequentialEventHandler<TSender, TArg> handler ) => eventHost.Add( handler );

        /// <summary>
        /// Relays to <see cref="Remove"/>.
        /// </summary>
        /// <param name="eventHost">The host.</param>
        /// <param name="handler">The non null handler to remove.</param>
        /// <returns>The host.</returns>
        public static SequentialEventHandlerSender<TSender, TArg> operator -( SequentialEventHandlerSender<TSender, TArg> eventHost, SequentialEventHandler<TSender, TArg> handler ) => eventHost.Remove( handler );

        /// <summary>
        /// Clears the delegate list.
        /// </summary>
        public void RemoveAll() => _handler = null;

        /// <summary>
        /// Raises this event.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The event argument.</param>
        public void Raise( IActivityMonitor monitor, TSender sender, TArg args )
        {
            var h = _handler;
            if( h == null ) return;
            if( h is SequentialEventHandler<TSender, TArg> a ) a( monitor, sender, args );
            else
            {
                var all = (SequentialEventHandler<TSender, TArg>[])h;
                foreach( var x in all ) x( monitor, sender, args );
            }
        }
    }
}
