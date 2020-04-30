using CK.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT.Common.Stores
{
	public class MemoryPacketStore : IPacketStore
	{
		readonly Dictionary<ushort, StoredApplicationMessage> _storedApplicationMessages = new Dictionary<ushort, StoredApplicationMessage>();
		readonly HashSet<ushort> _orphansId = new HashSet<ushort>();
		public IEnumerable<StoredApplicationMessage> AllStoredMessages => _storedApplicationMessages.Values;

		public IEnumerable<ushort> OrphansPacketsId => _orphansId;

		public Task CloseAsync( IActivityMonitor m ) => Task.CompletedTask;

		public ValueTask<QualityOfService> DiscardMessageFromIdAsync( IActivityMonitor m, ushort packetId )
		{
			QualityOfService qos = _storedApplicationMessages[packetId].QualityOfService;
			if( !_storedApplicationMessages.Remove( packetId ) ) throw new KeyNotFoundException();//TODO: change api so it return a not found.
			return new ValueTask<QualityOfService>( qos );
		}

		public ValueTask<bool> FreePacketIdAsync( IActivityMonitor m, ushort packetId ) => new ValueTask<bool>( _orphansId.Remove( packetId ) );

		ushort _i = 0;
		public ValueTask<ushort> GetNewPacketId( IActivityMonitor m )
		{
			while( _storedApplicationMessages.ContainsKey( _i ) && _orphansId.Contains( _i ) )
			{
				_i++;
			}
			return new ValueTask<ushort>( _i++ );
		}

		public async ValueTask<ushort> StoreMessageAsync( IActivityMonitor m, ApplicationMessage message, QualityOfService qos )
		{
			ushort id = await GetNewPacketId( m );
			_storedApplicationMessages.Add( id, new StoredApplicationMessage( message, qos, id ) );
			return id;
		}

		public ValueTask<bool> StorePacketIdAsync( IActivityMonitor m, ushort packetId ) => new ValueTask<bool>( _orphansId.Add( packetId ) );
	}
}
