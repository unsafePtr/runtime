// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Diagnostics.Metrics
{
    /// <summary>
    /// A struct-based enumerable for returning a small, fixed-maximum number of <see cref="Measurement{T}"/>
    /// values from an observable instrument callback without heap allocation. The storage lives entirely
    /// on the stack via an inline array of <see cref="Measurement{T}"/>.
    /// </summary>
    /// <remarks>
    /// Add measurements via <see cref="Add"/>, then return the struct from the callback by value.
    /// The struct is copied on return (including its inline storage) and consumed by the runtime via
    /// its concrete <see cref="GetEnumerator"/> method, which returns a struct enumerator. No boxing
    /// occurs if the caller iterates through the concrete type.
    /// </remarks>
    public struct InlineMeasurementEnumerable<T> : IEnumerable<Measurement<T>> where T : struct
    {
        public const int MaxCapacity = 8;

        private Slots _slots;
        private int _count;

#if NET8_0_OR_GREATER
        public InlineMeasurementEnumerable(params ReadOnlySpan<Measurement<T>> measurements)
        {
            if (measurements.Length > MaxCapacity)
            {
                ThrowCapacityExceeded();
            }
            for (int i = 0; i < measurements.Length; i++)
            {
                _slots[i] = measurements[i];
            }
            _count = measurements.Length;
        }
#endif

        public void Add(Measurement<T> measurement)
        {
            if (_count >= MaxCapacity)
            {
                ThrowCapacityExceeded();
            }
            _slots[_count++] = measurement;
        }

        public readonly int Count => _count;

        /// <summary>
        /// Concrete struct-typed enumerator. C# foreach over an <see cref="InlineMeasurementEnumerable{T}"/>
        /// binds to this method via duck-typing and avoids boxing.
        /// </summary>
        internal Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<Measurement<T>> IEnumerable<Measurement<T>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static void ThrowCapacityExceeded() =>
            throw new InvalidOperationException(
                $"InlineMeasurementEnumerable<T> capacity exceeded. Maximum is {MaxCapacity}.");

        internal struct Enumerator : IEnumerator<Measurement<T>>
        {
            private InlineMeasurementEnumerable<T> _source;
            private int _index;

            internal Enumerator(InlineMeasurementEnumerable<T> source)
            {
                _source = source;
                _index = -1;
            }

            public Measurement<T> Current => _source._slots[_index];
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++_index < _source._count;
            public void Reset() => _index = -1;
            public void Dispose() { }
        }

#if NET8_0_OR_GREATER
        [InlineArray(MaxCapacity)]
        private struct Slots
        {
            private Measurement<T> _e;
        }
#else
        [StructLayout(LayoutKind.Sequential)]
        private struct Slots
        {
            internal Measurement<T> _0, _1, _2, _3, _4, _5, _6, _7;

            public Measurement<T> this[int i]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Unsafe.Add(ref _0, i);
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => Unsafe.Add(ref _0, i) = value;
            }
        }
#endif
    }
}
