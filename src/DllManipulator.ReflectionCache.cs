using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace UnityNativeTool.Internal
{
    public partial class DllManipulator
    {
        private static readonly Type[] DELEGATE_CTOR_PARAMETERS = { typeof(object), typeof(IntPtr) };
        private static readonly Type[] UNMANAGED_FUNCTION_POINTER_ATTRIBUTE_CTOR_PARAMETERS = { typeof(CallingConvention) };
        private static readonly Type[] MARSHAL_AS_ATTRIBUTE_CTOR_PARAMETERS = { typeof(UnmanagedType) };


        private static readonly Lazy<FieldInfo> Field_NativeFunctions = new Lazy<FieldInfo>(
            () => typeof(DllManipulator).GetField(nameof(_nativeFunctions), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<FieldInfo> Field_NativeFunctionDelegate = new Lazy<FieldInfo>(
            () => typeof(NativeFunction).GetField(nameof(NativeFunction.@delegate), BindingFlags.Public | BindingFlags.Instance));

        private static readonly Lazy<MethodInfo> Method_LoadTargetFunction = new Lazy<MethodInfo>(
            () => typeof(DllManipulator).GetMethod(nameof(LoadTargetFunction), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<FieldInfo> Field_NativeFunctionLoadLock = new Lazy<FieldInfo>(
            () => typeof(DllManipulator).GetField(nameof(_nativeFunctionLoadLock), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<MethodInfo> Method_WriteNativeCrashLog = new Lazy<MethodInfo>(
            () => typeof(DllManipulator).GetMethod(nameof(WriteNativeCrashLog), BindingFlags.NonPublic | BindingFlags.Static));

        /// <summary>
        /// <see cref="ReaderWriterLockSlim.EnterReadLock()"/>
        /// </summary>
        private static readonly Lazy<MethodInfo> Method_Rwls_EnterReadLock = new Lazy<MethodInfo>(
            () => typeof(ReaderWriterLockSlim).GetMethod(nameof(ReaderWriterLockSlim.EnterReadLock), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref="ReaderWriterLockSlim.ExitReadLock()"/>
        /// </summary>
        private static readonly Lazy<MethodInfo> Method_Rwls_ExitReadLock = new Lazy<MethodInfo>(
            () => typeof(ReaderWriterLockSlim).GetMethod(nameof(ReaderWriterLockSlim.ExitReadLock), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// DynamicMethod.CreateDynMethod()
        /// </summary>
        /// <note>
        /// Only on Mono
        /// </note>
        private static readonly Lazy<MethodInfo> Method_DynamicMethod_CreateDynMethod = new Lazy<MethodInfo>(
            () => typeof(DynamicMethod).GetMethod("CreateDynMethod", BindingFlags.NonPublic | BindingFlags.Instance));

        /// <summary>
        /// DynamicMethod.GetMethodDescriptor()
        /// </summary>
        /// <note>
        /// Only on .NET Core
        /// </note>
        private static readonly Lazy<MethodInfo> Method_DynamicMethod_GetMethodDescriptor = new Lazy<MethodInfo>(
            () => typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.NonPublic | BindingFlags.Instance));

        /// <summary>
        /// RuntimeHelpers._CompileMethod(..)
        /// </summary>
        /// <note>
        /// Only on .NET Core
        /// </note>
        private static readonly Lazy<MethodInfo> Method_RuntimeHelpers__CompileMethod = new Lazy<MethodInfo>(
            () => typeof(RuntimeHelpers).GetMethod("_CompileMethod", BindingFlags.NonPublic | BindingFlags.Static));

        /// <summary>
        /// RuntimeMethodHandle.m_value
        /// </summary>
        /// <note>
        /// Only on .NET Core
        /// </note>
        private static readonly Lazy<FieldInfo> Field_RuntimeMethodHandle_m_value = new Lazy<FieldInfo>(
            () => typeof(RuntimeMethodHandle).GetField("m_value", BindingFlags.NonPublic | BindingFlags.Instance));

        /// <summary>
        /// RuntimeMethodHandle.GetMethodInfo()
        /// </summary>
        /// <note>
        /// Only on .NET Core
        /// </note>
        private static readonly Lazy<MethodInfo> Method_RuntimeMethodHandle_GetMethodInfo = new Lazy<MethodInfo>(
            () => typeof(RuntimeMethodHandle).GetMethod("GetMethodInfo", BindingFlags.NonPublic | BindingFlags.Instance));
    }
}
