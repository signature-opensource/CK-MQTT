using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace CK.MQTT.Common.Stores
{
    public class PacketIdFactory
    {
        readonly BitArray _values = new BitArray( ushort.MaxValue - 1 );
        int _index;

        /// <summary>
        /// Get a new PacketID.
        /// Return 0 if no PacketId are available.
        /// </summary>
        /// <returns></returns>
        public ushort GetAvailablePacketId()
        {
            lock( _values )
            {
                for( ; _index < ushort.MaxValue; _index++ )
                {
                    if( !_values[_index] )
                    {
                        _values[_index] = true;
                        ushort output = (ushort)(_index + 1);
                        _index++;
                        return output;
                    }
                }
                for( _index = 0; _index < ushort.MaxValue; _index++ )
                {
                    if( !_values[_index] )
                    {
                        _values[_index] = true;
                        ushort output = (ushort)(_index + 1);
                        _index++;
                        return output;
                    }
                }
                return 0;
            }
        }
    }
}
