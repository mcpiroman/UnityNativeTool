using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using DllManipulator.Internal;

namespace DllManipulator
{
    public partial class DllManipulator
    {
        private static readonly Lazy<FieldInfo> NativeFunctionsField = new Lazy<FieldInfo>(
            () => typeof(DllManipulator).GetField(nameof(_nativeFunctions), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<FieldInfo> NativeFunctionDelegateField = new Lazy<FieldInfo>(
            () => typeof(NativeFunction).GetField(nameof(NativeFunction.@delegate), BindingFlags.Public | BindingFlags.Instance));

        private static readonly Lazy<MethodInfo> LoadTargetFunctionMethod = new Lazy<MethodInfo>(
            () => typeof(DllManipulator).GetMethod(nameof(LoadTargetFunction), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<FieldInfo> NativeFunctionLoadLockField = new Lazy<FieldInfo>(
            () => typeof(DllManipulator).GetField(nameof(_nativeFunctionLoadLock), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<MethodInfo> WriteNativeCrashLogMethod = new Lazy<MethodInfo>(
            () => typeof(DllManipulator).GetMethod(nameof(WriteNativeCrashLog), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<FieldInfo> ThreadsCallingNativesField = new Lazy<FieldInfo>(
           () => typeof(DllManipulator).GetField(nameof(_threadsCallingNatives), BindingFlags.NonPublic | BindingFlags.Static));

        /// <summary>
        /// ReaderWriterLockSlim.EnterReadLock()
        /// </summary>
        private static readonly Lazy<MethodInfo> RwlsEnterReadLocKMethod = new Lazy<MethodInfo>(
            () => typeof(ReaderWriterLockSlim).GetMethod(nameof(ReaderWriterLockSlim.EnterReadLock), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// ReaderWriterLockSlim.ExitReadLock()
        /// </summary>
        private static readonly Lazy<MethodInfo> RwlsExitReadLockMethod = new Lazy<MethodInfo>(
            () => typeof(ReaderWriterLockSlim).GetMethod(nameof(ReaderWriterLockSlim.ExitReadLock), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// Thread.get_CurrentThread()
        /// </summary>
        private static readonly Lazy<MethodInfo> Thread_getCurrentThreadMethod = new Lazy<MethodInfo>(
           () => typeof(Thread).GetProperty(nameof(Thread.CurrentThread), BindingFlags.Public | BindingFlags.Static).GetGetMethod());

        /// <summary>
        /// ConcurrentDictionary<Thread, int>.TryAdd(Thread key, int value)
        /// </summary>
        private static readonly Lazy<MethodInfo> ConcurrentDictionaryThreadIntTryAddMethod = new Lazy<MethodInfo>(
           () => typeof(ConcurrentDictionary<Thread, int>).GetMethod(nameof(ConcurrentDictionary<Thread, int>.TryAdd), BindingFlags.Public | BindingFlags.Instance));
    }
}
