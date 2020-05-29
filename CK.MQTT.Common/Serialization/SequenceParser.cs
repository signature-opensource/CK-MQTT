// Modified code from https://github.com/dotnet/runtime/commit/5c12e28775077cf8b585f48bc83137e931283634.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.MQTT.Serialization
{
    public ref partial struct SequenceParser<T> where T : unmanaged, IEquatable<T>
    {
        enum ParserState : byte
        {
            NoMoreData = 1,
            Error = 2,
            NoMoreDataMask = byte.MaxValue - 1
        }
        private SequencePosition _currentPosition;
        private SequencePosition _nextPosition;
        private ParserState _parserState;
        private readonly long _length;

        /// <summary>
        /// Create a <see cref="SequenceParser{T}"/> over the given <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public SequenceParser( ReadOnlySequence<T> sequence )
        {
            CurrentSpanIndex = 0;
            Consumed = 0;
            Sequence = sequence;
            _currentPosition = sequence.Start;
            _length = -1;
            CurrentSpan = sequence.FirstSpan;
            _nextPosition = new SequencePosition( sequence, CurrentSpan.Length );
            _parserState = CurrentSpan.Length > 0 ? default : ParserState.NoMoreData;
            if( (_parserState == ParserState.NoMoreData) && !sequence.IsSingleSegment )
            {
                _parserState = default;
                GetNextSpan();
            }
        }

        /// <summary>
        /// True when there is no more data in the <see cref="Sequence"/>.
        /// </summary>
        public readonly bool End => (_parserState & ParserState.NoMoreData) == ParserState.NoMoreData;

        /// <summary>
        /// The underlying <see cref="ReadOnlySequence{T}"/> for the reader.
        /// </summary>
        public readonly ReadOnlySequence<T> Sequence { get; }

        /// <summary>
        /// Gets the unread portion of the <see cref="Sequence"/>.
        /// </summary>
        /// <value>
        /// The unread portion of the <see cref="Sequence"/>.
        /// </value>
        public readonly ReadOnlySequence<T> UnreadSequence => Sequence.Slice( Position );

        /// <summary>
        /// The current position in the <see cref="Sequence"/>.
        /// </summary>
        public readonly SequencePosition Position
            => Sequence.GetPosition( CurrentSpanIndex, _currentPosition );

        /// <summary>
        /// The current segment in the <see cref="Sequence"/> as a span.
        /// </summary>
        public ReadOnlySpan<T> CurrentSpan { readonly get; private set; }

        /// <summary>
        /// The index in the <see cref="CurrentSpan"/>.
        /// </summary>
        public int CurrentSpanIndex { readonly get; private set; }

        /// <summary>
        /// The unread portion of the <see cref="CurrentSpan"/>.
        /// </summary>
        public readonly ReadOnlySpan<T> UnreadSpan
        {
            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            get => CurrentSpan.Slice( CurrentSpanIndex );
        }

        /// <summary>
        /// The total number of <typeparamref name="T"/>'s processed by the reader.
        /// </summary>
        public long Consumed { readonly get; private set; }

        /// <summary>
        /// Remaining <typeparamref name="T"/>'s in the reader's <see cref="Sequence"/>.
        /// </summary>
        public readonly long Remaining => Length - Consumed;

        /// <summary>
        /// Count of <typeparamref name="T"/> in the reader's <see cref="Sequence"/>.
        /// </summary>
        public readonly long Length
        {
            get
            {
                if( _length < 0 )
                {
                    // Cast-away readonly to initialize lazy field
                    Volatile.Write( ref Unsafe.AsRef( _length ), Sequence.Length );
                }
                return _length;
            }
        }

        public readonly bool HasError => (_parserState & ParserState.Error) == ParserState.Error;

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        void SetNoMoreData() => _parserState |= ParserState.NoMoreData;

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        void SetMoreData() => _parserState &= ParserState.NoMoreDataMask;

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void SetError()
        {
            _parserState |= ParserState.Error;
        }

        /// <summary>
        /// Peeks at the next value without advancing the reader.
        /// </summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public readonly bool TryPeek( out T value )
        {
            if( !End )
            {
                value = CurrentSpan[CurrentSpanIndex];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Peeks at the next value without advancing the reader.
        /// If at the end of the reader, return default and <see cref="HasError"/> will be set to <see langword="true"/>.
        /// </summary>
        /// <returns>The next value, or the default value if at the end of the reader.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Peek()
        {
            if( !TryPeek( out T value ) ) SetError();
            return value;
        }

        /// <summary>
        /// Peeks at the next value at specific offset without advancing the reader.
        /// </summary>
        /// <param name="offset">The offset from current position.</param>
        /// <param name="value">The next value, or the default value if at the end of the reader.</param>
        /// <returns><c>true</c> if the reader is not at its end and the peek operation succeeded; <c>false</c> if at the end of the reader.</returns>
        public readonly bool TryPeek( long offset, out T value )
        {
            if( offset < 0 ) throw new ArgumentOutOfRangeException( nameof( offset ) );

            // If we've got data and offset is not out of bounds
            if( End || Remaining <= offset )
            {
                value = default;
                return false;
            }

            // Sum CurrentSpanIndex + offset could overflow as is but the value of offset should be very large
            // because we check Remaining <= offset above so to overflow we should have a ReadOnlySequence close to 8 exabytes
            Debug.Assert( CurrentSpanIndex + offset >= 0 );

            // If offset doesn't fall inside current segment move to next until we find correct one
            if( (CurrentSpanIndex + offset) <= CurrentSpan.Length - 1 )
            {
                Debug.Assert( offset <= int.MaxValue );

                value = CurrentSpan[CurrentSpanIndex + (int)offset];
                return true;
            }
            else
            {
                long remainingOffset = offset - (CurrentSpan.Length - CurrentSpanIndex);
                SequencePosition nextPosition = _nextPosition;
                ReadOnlyMemory<T> currentMemory = default;

                while( Sequence.TryGet( ref nextPosition, out currentMemory, advance: true ) )
                {
                    // Skip empty segment
                    if( currentMemory.Length > 0 )
                    {
                        if( remainingOffset >= currentMemory.Length )
                        {
                            // Subtract current non consumed data
                            remainingOffset -= currentMemory.Length;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                value = currentMemory.Span[(int)remainingOffset];
                return true;
            }
        }

        /// <summary>
        /// Peeks at the next value at specific offset without advancing the reader.
        /// If at the end of the reader, return default and <see cref="HasError"/> will be set to <see langword="true"/>.
        /// </summary>
        /// <param name="offset">The offset from current position.</param>
        /// <returns>The next value, or the default value if at the end of the reader.</returns>
        public T Peek( long offset )
        {
            if( !TryPeek( offset, out T value ) ) SetError();
            return value;
        }

        /// <summary>
        /// Read the next value and advance the reader.
        /// </summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool TryRead( out T value )
        {
            if( End )
            {
                value = default;
                return false;
            }

            value = CurrentSpan[CurrentSpanIndex];
            CurrentSpanIndex++;
            Consumed++;

            if( CurrentSpanIndex >= CurrentSpan.Length )
            {
                GetNextSpan();
            }

            return true;
        }

        /// <summary>
        /// Read the next value and advance the reader.
        /// If at the end of the reader, return default and <see cref="HasError"/> will be set to <see langword="true"/>.
        /// </summary>
        /// <returns>The next value or default if at the end.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Read()
        {
            if( !TryRead( out T value ) ) SetError();
            return value;
        }

        /// <summary>
        /// Move the reader back the specified number of items.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if trying to rewind a negative amount or more than <see cref="Consumed"/>.
        /// </exception>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Rewind( long count )
        {
            if( (ulong)count > (ulong)Consumed )
            {
                throw new ArgumentOutOfRangeException( nameof( count ) );
            }

            Consumed -= count;

            if( CurrentSpanIndex >= count )
            {
                CurrentSpanIndex -= (int)count;
                SetMoreData();
            }
            else
            {
                // Current segment doesn't have enough data, scan backward through segments
                RetreatToPreviousSpan( Consumed );
            }
        }

        [MethodImpl( MethodImplOptions.NoInlining )]
        private void RetreatToPreviousSpan( long consumed )
        {
            ResetReader();
            Advance( consumed );
        }

        private void ResetReader()
        {
            CurrentSpanIndex = 0;
            Consumed = 0;
            _currentPosition = Sequence.Start;
            _nextPosition = _currentPosition;

            if( Sequence.TryGet( ref _nextPosition, out ReadOnlyMemory<T> memory, advance: true ) )
            {
                SetMoreData();
                if( memory.Length == 0 )
                {
                    CurrentSpan = default;
                    // No data in the first span, move to one with data
                    GetNextSpan();
                }
                else
                {
                    CurrentSpan = memory.Span;
                }
            }
            else
            {
                // No data in any spans and at end of sequence
                SetNoMoreData();
                CurrentSpan = default;
            }
        }

        /// <summary>
        /// Get the next segment with available data, if any.
        /// </summary>
        private void GetNextSpan()
        {
            if( !Sequence.IsSingleSegment )
            {
                SequencePosition previousNextPosition = _nextPosition;
                while( Sequence.TryGet( ref _nextPosition, out ReadOnlyMemory<T> memory, advance: true ) )
                {
                    _currentPosition = previousNextPosition;
                    if( memory.Length > 0 )
                    {
                        CurrentSpan = memory.Span;
                        CurrentSpanIndex = 0;
                        return;
                    }
                    else
                    {
                        CurrentSpan = default;
                        CurrentSpanIndex = 0;
                        previousNextPosition = _nextPosition;
                    }
                }
            }
            SetNoMoreData();
        }

        /// <summary>
        /// Move the reader ahead the specified number of items.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Advance( long count )
        {
            const long TooBigOrNegative = unchecked((long)0xFFFFFFFF80000000);
            if( (count & TooBigOrNegative) == 0 && CurrentSpan.Length - CurrentSpanIndex > (int)count )
            {
                CurrentSpanIndex += (int)count;
                Consumed += count;
            }
            else
            {
                // Can't satisfy from the current span
                AdvanceToNextSpan( count );
            }
        }

        /// <summary>
        /// Unchecked helper to avoid unnecessary checks where you know count is valid.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal void AdvanceCurrentSpan( long count )
        {
            Debug.Assert( count >= 0 );

            Consumed += count;
            CurrentSpanIndex += (int)count;
            if( CurrentSpanIndex >= CurrentSpan.Length ) GetNextSpan();
        }

        /// <summary>
        /// Only call this helper if you know that you are advancing in the current span
        /// with valid count and there is no need to fetch the next one.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal void AdvanceWithinSpan( long count )
        {
            Debug.Assert( count >= 0 );

            Consumed += count;
            CurrentSpanIndex += (int)count;

            Debug.Assert( CurrentSpanIndex < CurrentSpan.Length );
        }

        private void AdvanceToNextSpan( long count )
        {
            if( count < 0 ) throw new ArgumentOutOfRangeException( nameof( count ) );

            Consumed += count;
            while( !End )
            {
                int remaining = CurrentSpan.Length - CurrentSpanIndex;

                if( remaining > count )
                {
                    CurrentSpanIndex += (int)count;
                    count = 0;
                    break;
                }

                // As there may not be any further segments we need to
                // push the current index to the end of the span.
                CurrentSpanIndex += remaining;
                count -= remaining;
                Debug.Assert( count >= 0 );

                GetNextSpan();

                if( count == 0 ) break;
            }

            if( count != 0 )
            {
                // Not enough data left- adjust for where we actually ended and throw
                Consumed -= count;
                SetError();
            }
        }

        /// <summary>
        /// Copies data from the current <see cref="Position"/> to the given <paramref name="destination"/> span if there
        /// is enough data to fill it.
        /// </summary>
        /// <remarks>
        /// This API is used to copy a fixed amount of data out of the sequence if possible. It does not advance
        /// the reader. To look ahead for a specific stream of data <see cref="IsNext(ReadOnlySpan{T}, bool)"/> can be used.
        /// </remarks>
        /// <param name="destination">Destination span to copy to.</param>
        /// <returns>True if there is enough data to completely fill the <paramref name="destination"/> span.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public readonly bool TryCopyTo( Span<T> destination )
        {
            // This API doesn't advance to facilitate conditional advancement based on the data returned.
            // We don't provide an advance option to allow easier utilizing of stack allocated destination spans.
            // (Because we can make this method readonly we can guarantee that we won't capture the span.)

            ReadOnlySpan<T> firstSpan = UnreadSpan;
            if( firstSpan.Length >= destination.Length )
            {
                firstSpan.Slice( 0, destination.Length ).CopyTo( destination );
                return true;
            }

            // Not enough in the current span to satisfy the request, fall through to the slow path
            return TryCopyMultisegment( destination );
        }

        internal readonly bool TryCopyMultisegment( Span<T> destination )
        {
            // If we don't have enough to fill the requested buffer, return false
            if( Remaining < destination.Length )
                return false;

            ReadOnlySpan<T> firstSpan = UnreadSpan;
            Debug.Assert( firstSpan.Length < destination.Length );
            firstSpan.CopyTo( destination );
            int copied = firstSpan.Length;

            SequencePosition next = _nextPosition;
            while( Sequence.TryGet( ref next, out ReadOnlyMemory<T> nextSegment, true ) )
            {
                if( nextSegment.Length > 0 )
                {
                    ReadOnlySpan<T> nextSpan = nextSegment.Span;
                    int toCopy = Math.Min( nextSpan.Length, destination.Length - copied );
                    nextSpan.Slice( 0, toCopy ).CopyTo( destination.Slice( copied ) );
                    copied += toCopy;
                    if( copied >= destination.Length )
                    {
                        break;
                    }
                }
            }

            return true;
        }

        internal void CopyMultisegment( Span<T> destination)
        {
            if( !TryCopyMultisegment( destination ) ) SetError();
        }
    }
}
