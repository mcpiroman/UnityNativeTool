using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using DllManipulator.Internal;

namespace DllManipulator
{
    public partial class DllManipulator
    {
        private static readonly Type[] DELEGATE_CTOR_PARAMETERS = { typeof(object), typeof(IntPtr) };
        private static readonly Type[] UNMANAGED_FUNCTION_POINTER_ATTRIBUTE_CTOR_PARAMETERS = { typeof(CallingConvention) };
        private static readonly Type[] MARSHAL_AS_ATTRIBUTE_CTOR_PARAMETERS = { typeof(UnmanagedType) };


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
    }
}
