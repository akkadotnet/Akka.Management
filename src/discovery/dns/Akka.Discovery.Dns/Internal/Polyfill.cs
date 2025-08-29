#if NETSTANDARD2_0
//this is required for compiling C# records for netstandard2_0 and other older targets 
namespace System.Runtime.CompilerServices;
internal static class IsExternalInit {}

#endif