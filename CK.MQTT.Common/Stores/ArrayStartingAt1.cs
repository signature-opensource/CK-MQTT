using System;

namespace CK.MQTT.Stores
{
    public struct ArrayStartingAt1<T>
    {
        readonly T[] _array;
        public ArrayStartingAt1( T[] array )
        {
            _array = array;
        }
        public ref T this[int index] => ref _array[index - 1];

        public int Length => _array.Length;

        public void CopyTo( ArrayStartingAt1<T> other, int index ) => _array.CopyTo( other._array, index );

        public void Clear() => Array.Clear( _array, 0, _array.Length );
    }
}
