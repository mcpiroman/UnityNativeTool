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
        /// <see cref=UnmanagedFunctionPointerAttribute"/>
        /// </summary>
        private static readonly Lazy<ConstructorInfo> Ctor_Ufp = new Lazy<ConstructorInfo>(
            () => typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) }));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.BestFitMapping"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_BestFitMapping = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.BestFitMapping), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.CharSet"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_CharSet = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.CharSet), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.SetLastError"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_SetLastError = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.SetLastError), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.ThrowOnUnmappableChar"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_ThrowOnUnmappableChar = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.ThrowOnUnmappableChar), BindingFlags.Public | BindingFlags.Instance));


        #region Mono specific

        /// <summary>
        /// DynamicMethod.CreateDynMethod()
        /// </summary>
        private static readonly Lazy<MethodInfo> Method_DynamicMethod_CreateDynMethod = new Lazy<MethodInfo>(
            () => typeof(DynamicMethod).GetMethod("CreateDynMethod", BindingFlags.NonPublic | BindingFlags.Instance));

        private static readonly Lazy<FieldInfo> Field_IlGenerator_token_gen = new Lazy<FieldInfo>(
            () => typeof(ILGenerator).GetField("token_gen", BindingFlags.NonPublic | BindingFlags.Instance));

        private static readonly Lazy<FieldInfo> Field_IlGenerator_cur_stack = new Lazy<FieldInfo>(
            () => typeof(ILGenerator).GetField("cur_stack", BindingFlags.NonPublic | BindingFlags.Instance));

        private static readonly Lazy<MethodInfo> Method_IlGenerator_make_room = new Lazy<MethodInfo>(
            () => typeof(ILGenerator).GetMethod("make_room", BindingFlags.NonPublic | BindingFlags.Instance));

        private static readonly Lazy<MethodInfo> Method_IlGenerator_ll_emit = new Lazy<MethodInfo>(
           () => typeof(ILGenerator).GetMethod("ll_emit", BindingFlags.NonPublic | BindingFlags.Instance));

        private static readonly Lazy<MethodInfo> Method_IlGenerator_emit_int = new Lazy<MethodInfo>(
            () => typeof(ILGenerator).GetMethod("emit_int", BindingFlags.NonPublic | BindingFlags.Instance));

        private static readonly Lazy<MethodInfo> Method_IlGenerator_add_token_fixup = new Lazy<MethodInfo>(
            () => typeof(ILGenerator).GetMethod("add_token_fixup", BindingFlags.NonPublic | BindingFlags.Instance));

        private static readonly Lazy<Type> Type_TokenGenerator = new Lazy<Type>(
           () => typeof(ILGenerator).Assembly.GetType("System.Reflection.Emit.TokenGenerator", true, false));

        /// <summary>
        /// TokenGenerator.GetToken(MethodInfo, bool)
        /// </summary>
        private static readonly Lazy<MethodInfo> Method_TokenGenerator_GetToken = new Lazy<MethodInfo>(
           () => Type_TokenGenerator.Value.GetMethod("GetToken", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(MethodInfo), typeof(bool) }, null));

        private static readonly Lazy<Type> Type_MethodOnTypeBuilderInst = new Lazy<Type>(
           () => typeof(ILGenerator).Assembly.GetType("System.Reflection.Emit.MethodOnTypeBuilderInst", true, false));

        #endregion
    }
}
