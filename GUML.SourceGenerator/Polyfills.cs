// Polyfills for netstandard2.0 compatibility with C# 12+ language features.
// These types are required by the compiler when using 'init' properties,
// range/index operators, and other modern C# syntax on older target frameworks.

#if NETSTANDARD2_0

using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler polyfill: enables 'init' accessor support on netstandard2.0.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}

// ReSharper disable once CheckNamespace
namespace System
{
    /// <summary>
    /// Compiler polyfill: represents a type that can be used to index a collection
    /// either from the start or the end. Enables <c>^n</c> syntax.
    /// </summary>
    internal readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            _value = fromEnd ? ~value : value;
        }

        public static Index Start => new Index(0);
        public static Index End => new Index(0, true);

        public static Index FromStart(int value) => new Index(value);
        public static Index FromEnd(int value) => new Index(value, true);

        public int Value => _value < 0 ? ~_value : _value;
        public bool IsFromEnd => _value < 0;

        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;

        public static implicit operator Index(int value) => FromStart(value);

        public bool Equals(Index other) => _value == other._value;
        public override bool Equals(object? obj) => obj is Index other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsFromEnd ? $"^{Value}" : Value.ToString();
    }

    /// <summary>
    /// Compiler polyfill: represents a range that has start and end indices.
    /// Enables <c>start..end</c> syntax.
    /// </summary>
    internal readonly struct Range : IEquatable<Range>
    {
        public Index Start { get; }
        public Index End { get; }

        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public static Range StartAt(Index start) => new Range(start, Index.End);
        public static Range EndAt(Index end) => new Range(Index.Start, end);
        public static Range All => new Range(Index.Start, Index.End);

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);
            return (start, end - start);
        }

        public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);
        public override bool Equals(object? obj) => obj is Range other && Equals(other);
        public override int GetHashCode() => Start.GetHashCode() * 31 + End.GetHashCode();
        public override string ToString() => $"{Start}..{End}";
    }
}

// Required for collection expressions targeting Span/ReadOnlySpan on netstandard2.0.
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler polyfill: required for collection expression support with inline arrays.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    internal sealed class CollectionBuilderAttribute : Attribute
    {
        public CollectionBuilderAttribute(Type builderType, string methodName)
        {
            BuilderType = builderType;
            MethodName = methodName;
        }

        public Type BuilderType { get; }
        public string MethodName { get; }
    }
}

// ReSharper disable once CheckNamespace
namespace System.Collections.Generic
{
    /// <summary>
    /// Polyfill: provides Dictionary.TryAdd which is unavailable in netstandard2.0.
    /// </summary>
    internal static class DictionaryExtensions
    {
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
                return false;
            dictionary.Add(key, value);
            return true;
        }
    }
}

#endif
