using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
	public class MemoryPacketStore : PacketStore
	{
		readonly Dictionary<ushort, Memory<byte>> _storedApplicationMessages = new Dictionary<ushort, Memory<byte>>();
		readonly HashSet<ushort> _orphansId = new HashSet<ushort>();
		public ValueTask<QualityOfService> DiscardMessageByIdAsync( IActivityMonitor m, ushort packetId )
		{
			QualityOfService qos = _storedApplicationMessages[packetId].QualityOfService;
			if( !_storedApplicationMessages.Remove( packetId ) ) throw new KeyNotFoundException();//TODO: change api so it return a not found.
			return new ValueTask<QualityOfService>( qos );
		}

		public ValueTask<bool> DiscardPacketIdAsync( IActivityMonitor m, ushort packetId ) => new ValueTask<bool>( _orphansId.Remove( packetId ) );

		ushort _i = 0;
		public ValueTask<ushort> GetNewPacketId( IActivityMonitor m )
		{
			while( _storedApplicationMessages.ContainsKey( _i ) && _orphansId.Contains( _i ) )
			{
				_i++;
			}
			return new ValueTask<ushort>( _i++ );
		}

		public async ValueTask<ushort> StoreMessageAsync( IActivityMonitor m, OutgoingApplicationMessage message, QualityOfService qos )
		{
			ushort id = await GetNewPacketId( m );
			_storedApplicationMessages.Add( id, new StoredApplicationMessage( message, qos, id ) );
			return id;
		}

		public ValueTask<bool> StorePacketIdAsync( IActivityMonitor m, ushort packetId ) => new ValueTask<bool>( _orphansId.Add( packetId ) );
	}
}
