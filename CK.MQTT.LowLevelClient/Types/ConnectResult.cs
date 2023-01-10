using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.IO.Pipelines;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Threading;
using CK.MQTT.Pumps;
using CK.MQTT.LowLevelClient;
using System.Threading.Tasks;
using CK.MQTT.Client;

namespace CK.MQTT
{
    /// <summary>
    /// Represent the result of a connect operation.
    /// </summary>
    public readonly struct ConnectResult
    {
        readonly bool _deffered;

        /// <summary>
        /// The error if the client could not connect.
        /// </summary>
        public readonly ConnectError Error;

        /// <summary>
        /// The state of the sessions.
        /// </summary>
        public readonly SessionState SessionState;

        /// <summary>
        /// The connect return code.
        /// </summary>
        public readonly ProtocolConnectReturnCode ProtocolReturnCode;

        /// <summary>
        /// Not null when <see cref="Error"/> is <see cref="ConnectError.InternalException"/>.
        /// Contain the exception that was throwed when connecting.
        /// </summary>
        public Exception? Exception { get; }


        /// <summary>
        /// Indicate whether the a connection error may be recoverable or not.
        /// </summary>
        /// <remarks>
        /// This is default logic, it may not be correct for your use case.
        /// For example, <see cref="ProtocolConnectReturnCode.ServerUnavailable"/> is defined as <see cref="ConnectStatus.ErrorMaybeRecoverable"/>.
        /// </remarks>
        public ConnectStatus Status => _deffered ?
            ConnectStatus.Deferred
            : ProtocolReturnCode switch
            {
                ProtocolConnectReturnCode.Accepted => ConnectStatus.Successful,
                ProtocolConnectReturnCode.BadUserNameOrPassword => ConnectStatus.ErrorUnrecoverable,
                ProtocolConnectReturnCode.IdentifierRejected => ConnectStatus.ErrorUnrecoverable,
                ProtocolConnectReturnCode.NotAuthorized => ConnectStatus.ErrorUnrecoverable,
                ProtocolConnectReturnCode.ServerUnavailable => ConnectStatus.ErrorMaybeRecoverable,
                ProtocolConnectReturnCode.UnacceptableProtocolVersion => ConnectStatus.ErrorUnrecoverable,
                ProtocolConnectReturnCode.Unknown => Error switch
                {
                    ConnectError.UserCancelled => ConnectStatus.ErrorMaybeRecoverable,
                    ConnectError.InternalException => ConnectStatus.ErrorUnknown,
                    ConnectError.None => throw new InvalidOperationException( "This code path should not be hit. 1" ),
                    ConnectError.SeeReturnCode => throw new InvalidOperationException( "This code path should not be hit. 2" ),
                    ConnectError.ProtocolError => ConnectStatus.ErrorMaybeRecoverable,
                    ConnectError.Timeout => ConnectStatus.ErrorMaybeRecoverable,
                    ConnectError.Disconnected => ConnectStatus.ErrorMaybeRecoverable,
                    _ => throw new InvalidOperationException( $"Invalid {nameof( Error )}:{Error}." )
                },
                _ => throw new InvalidOperationException( $"Invalid {nameof( ProtocolReturnCode )}:{ProtocolReturnCode}." )
            };

        /// <summary>
        /// Instantiate a new <see cref="ConnectResult"/> where the result is an error.
        /// <see cref="SessionState"/> will be <see cref="SessionState.Unknown"/>.
        /// <see cref="ProtocolReturnCode"/> will be <see cref="ProtocolConnectReturnCode.Unknown"/>.
        /// </summary>
        /// <param name="connectError">The reason the client could not connect.</param>
        public ConnectResult( ConnectError connectError )
        {
            Debug.Assert( connectError != ConnectError.SeeReturnCode );
            Error = connectError;
            Exception = null;
            SessionState = SessionState.Unknown;
            ProtocolReturnCode = ProtocolConnectReturnCode.Unknown;
            _deffered = false;
        }

        /// <summary>
        /// Instantiate a new <see cref="ConnectResult"/> where the result is an internal exception.
        /// <see cref="SessionState"/> will be <see cref="SessionState.Unknown"/>.
        /// <see cref="ProtocolReturnCode"/> will be <see cref="ProtocolConnectReturnCode.Unknown"/>.
        /// </summary>
        /// <param name="exception"> The exception that caused the internal exception.</param>
        public ConnectResult( Exception? exception = null )
        {
            Error = ConnectError.InternalException;
            Exception = exception;
            SessionState = SessionState.Unknown;
            ProtocolReturnCode = ProtocolConnectReturnCode.Unknown;
            _deffered = false;
        }

        /// <summary>
        /// Instantiate a new <see cref="ConnectResult"/> where the client got connected.
        /// <see cref="Error"/> will be <see cref="ConnectError.None"/>.
        /// </summary>
        /// <param name="sessionState">The session state.</param>
        /// <param name="connectReturnCode">The connection return code.</param>
        public ConnectResult( SessionState sessionState, ProtocolConnectReturnCode connectReturnCode )
        {
            Exception = null;
            Error = connectReturnCode == ProtocolConnectReturnCode.Accepted ? ConnectError.None : ConnectError.SeeReturnCode;
            SessionState = sessionState;
            ProtocolReturnCode = connectReturnCode;
            _deffered = false;
        }

        /// <summary>
        /// Constructor to sets all field manually.
        /// </summary>
        public ConnectResult( bool deffered, ConnectError error, SessionState sessionState, ProtocolConnectReturnCode protocolReturnCode, Exception? exception )
        {
            _deffered = deffered;
            Error = error;
            SessionState = sessionState;
            ProtocolReturnCode = protocolReturnCode;
            Exception = exception;
        }

        /// <inheritdoc/>
        public override bool Equals( object? obj )
            => obj is ConnectResult result
            && result.Error == Error
            && result.ProtocolReturnCode == ProtocolReturnCode
            && result.SessionState == SessionState;

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine( SessionState, Error, ProtocolReturnCode );

        /// <inheritdoc/>
        public static bool operator ==( ConnectResult left, ConnectResult right ) => left.Equals( right );

        /// <inheritdoc/>
        public static bool operator !=( ConnectResult left, ConnectResult right ) => !(left == right);

        /// <summary>
        /// Return a string containing various 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
            => $"ConnectResult(Status:'{Status}' ReturnCode:'{ProtocolReturnCode}' Deffered:'{_deffered}' ConnectError:'{Error}' SessionState:'{SessionState}')";

        internal static async ValueTask<ConnectResult> ReadAsync( PipeReader reader, IMQTT3Sink sink, CancellationToken cancellationToken )
        {
            try
            {
                byte header;
                uint length;
                while( true )
                {
                    var res = await reader.ReadAsync( cancellationToken );
                    var status = InputPump.TryParsePacketHeader( res.Buffer, out header, out length, out var headerPos );
                    if( status == OperationStatus.Done )
                    {
                        reader.AdvanceTo( headerPos );
                        break;
                    }
                    if( cancellationToken.IsCancellationRequested || res.IsCanceled ) return new ConnectResult( ConnectError.UserCancelled );
                    if( res.IsCompleted ) break;
                    reader.AdvanceTo( res.Buffer.Start, headerPos );
                }
                if( header != (byte)PacketType.ConnectAck )
                {
                    return new ConnectResult( ConnectError.ProtocolError );
                }
                var read = await reader.ReadAtLeastAsync( 2, cancellationToken );
                if( read.Buffer.Length < 2 ) return new ConnectResult( ConnectError.Disconnected );
                Deserialize( read.Buffer, out byte state, out byte code, out SequencePosition position );
                reader.AdvanceTo( position );
                if( state > 1 )
                {
                    return new ConnectResult( ConnectError.ProtocolError );
                }
                if( code > 5 )
                {
                    return new ConnectResult( ConnectError.ProtocolError );
                }
                if( length > 2 )
                {
                    await reader.UnparsedExtraDataAsync( sink, 0, (ushort)(length - 2), cancellationToken );
                }
                return new ConnectResult( (SessionState)state, (ProtocolConnectReturnCode)code );
            }
            catch( EndOfStreamException )
            {
                return new ConnectResult( ConnectError.ProtocolError );
            }
            catch( Exception )
            {
                return new ConnectResult( ConnectError.InternalException );
            }
        }

        static void Deserialize( ReadOnlySequence<byte> buffer, out byte state, out byte code, out SequencePosition position )
        {
            SequenceReader<byte> reader = new( buffer );
            bool res = reader.TryRead( out state );
            bool res2 = reader.TryRead( out code );
            position = reader.Position;
            Debug.Assert( res && res2 );
        }
    }
}
