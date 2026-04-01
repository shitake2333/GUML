// Required by the C# compiler to enable 'init' accessors and 'record' types on netstandard2.0.
// Record primary constructors generate 'init'-only properties; without this class the
// compiler cannot emit the required IL.

#if NETSTANDARD2_0

using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler polyfill: enables 'init' accessor and 'record' type support on netstandard2.0.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit;
}

#endif
