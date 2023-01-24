using System;

namespace CK.MQTT.Stores
{
    public readonly struct ArrayStartingAt1<T> //TODO: benchmark, it may be way faster to just not use the first slot of the array.
    {
        readonly T[] _array;
        public ArrayStartingAt1( T[] array ) => _array = array;
        public ref T this[uint index] => ref _array[index - 1];
        public ref T this[int index] => ref _array[index - 1];

        public int Length => _array.Length;

        public void CopyTo( ArrayStartingAt1<T> other, int index ) => _array.CopyTo( other._array, index );

        public void Clear() => Array.Clear( _array, 0, _array.Length );

        public override bool Equals( object? obj ) => throw new NotSupportedException();

        public override int GetHashCode() => throw new NotSupportedException();

        public static bool operator ==( ArrayStartingAt1<T> left, ArrayStartingAt1<T> right ) => left.Equals( right );

        public static bool operator !=( ArrayStartingAt1<T> left, ArrayStartingAt1<T> right ) => !(left == right);
    }
}
