//using CK.Core;
//using System;
//using System.Buffers;
//using System.Collections.Generic;
//using System.IO.Pipelines;
//using System.Text;
//using System.Threading.Tasks;

//namespace CK.MQTT.Common.Packets
//{
//    class IncomingConnect : Connect
//    {
//        IncomingConnect(
//            string clientId,
//            bool cleanSession,
//            ushort keepAlive,
//            IncomingLastWill lastWill,
//            string? userName,
//            string? password,
//            string protocolName = "MQTT")
//            : base( clientId, cleanSession, keepAlive, userName, password, protocolName )
//        {
//            LastWill = lastWill;
//        }

//        public IncomingLastWill LastWill { get; }

//        public ValueTask<IncomingConnect?> Deserialize(
//            IActivityMonitor m, ReadOnlySequence<byte> sequence, bool allowInvalidMagic)
//        {
//            
//        }
//    }
//}
