#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for init-only setters in netstandard2.1.
    /// </summary>
    internal static class IsExternalInit { }
}
#endif
