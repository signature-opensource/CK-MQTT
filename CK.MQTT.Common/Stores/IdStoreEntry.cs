using System;

namespace CK.MQTT.Common.Stores
{
    public struct IdStoreEntry<T>
    {
        public int NextId;
        public int PreviousId;
        public T Content;

        public override bool Equals( object? obj ) => throw new NotSupportedException();

        public override int GetHashCode() => throw new NotSupportedException();

        public static bool operator ==( IdStoreEntry<T> left, IdStoreEntry<T> right ) => throw new NotSupportedException();

        public static bool operator !=( IdStoreEntry<T> left, IdStoreEntry<T> right ) => throw new NotSupportedException();
    }
}
