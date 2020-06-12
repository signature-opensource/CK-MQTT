//using System;
//using System.Collections.Generic;
//using System.IO.Pipelines;
//using System.Text;

//namespace CK.MQTT.Common.Packets
//{
//    class IncomingLastWill : LastWill
//    {
//        internal IncomingLastWill(
//            IncomingApplicationMessage message,
//            int payloadSize, QualityOfService qualityOfService, bool retain )
//            : base( qualityOfService, retain )
//        {
//            Payload = message;
//            PayloadSize = payloadSize;
//        }

//        public IncomingApplicationMessage Payload { get; }// Why a PipeReader ? Most of the time this will be written on disk.

//        public int PayloadSize { get; }
//    }
//}
