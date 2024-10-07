using System;

namespace CK.MQTT.Client;

public class ApplicationMessage
{
    Type? _disposingType;
    private readonly string _topic;
    private readonly ReadOnlyMemory<byte> _payload;
    private readonly QualityOfService _qoS;
    private readonly bool _retain;

    public ApplicationMessage( string topic, ReadOnlyMemory<byte> payload, QualityOfService qoS, bool retain )
    {
        _topic = topic;
        _payload = payload;
        _qoS = qoS;
        _retain = retain;
    }

    public string Topic => CheckDisposed( _topic );
    public ReadOnlyMemory<byte> Payload => CheckDisposed( _payload );
    public QualityOfService QoS => CheckDisposed( _qoS );
    public bool Retain => CheckDisposed( _retain );
    T CheckDisposed<T>( T val )
    {
        if( _disposingType != null ) throw new ObjectDisposedException( _disposingType.ToString() );
        return val;
    }

    internal void SetDisposed( Type disposingType )
    {
        CheckDisposed<object>( null! );
        _disposingType = disposingType;
    }

    public override bool Equals( object? obj )
        => obj is ApplicationMessage message &&
               Topic == message.Topic &&
               Payload.Span.SequenceEqual( message.Payload.Span ) &&
               QoS == message.QoS &&
               Retain == message.Retain;

    public override int GetHashCode() => HashCode.Combine( Topic, Payload, QoS, Retain );

    public static bool operator ==( ApplicationMessage left, ApplicationMessage right )
        => left.Equals( right );

    public static bool operator !=( ApplicationMessage left, ApplicationMessage right )
        => !(left == right);
}
