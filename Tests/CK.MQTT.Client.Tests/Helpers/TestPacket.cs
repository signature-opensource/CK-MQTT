using System;

namespace CK.MQTT.Client.Tests.Helpers
{
    struct TestPacket
    {
        public ReadOnlyMemory<byte> Buffer;
        public PacketDirection PacketDirection;
        public TimeSpan OperationTime;
        public TestPacket( ReadOnlyMemory<byte> buffer, PacketDirection packetDirection, TimeSpan operationTime )
        {
            Buffer = buffer;
            PacketDirection = packetDirection;
            OperationTime = operationTime;
        }

        public static TestPacket Incoming( string hexArray, TimeSpan operationTime = default )
            => new( FromString( hexArray ), PacketDirection.ToClient, operationTime );
        public static TestPacket Outgoing( string hexArray, TimeSpan operationTime = default )
            => new( FromString( hexArray ), PacketDirection.ToServer, operationTime );

        public static ReadOnlyMemory<byte> FromString( string hexArray )
        {
            hexArray = hexArray.ToLower();
            byte[] arr = new byte[(hexArray.Length / 2)];
            for( int i = 0; i < hexArray.Length; i += 2 )
            {
                arr[i / 2] = Convert.ToByte( hexArray.Substring( i, 2 ), 16 );
            }
            return arr;
        }
    }
}
