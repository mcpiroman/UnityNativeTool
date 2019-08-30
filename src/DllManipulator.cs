using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.IO;
using UnityEngine;

namespace UnityNativeTool.Internal
{
    //Note "DLL" used in this code refers to Dynamically Loaded Library, and not to the .dll file extension on Windows.
    public partial class DllManipulator
    {
        public const string DLL_PATH_PATTERN_DLL_NAME_MACRO = "{name}";
        public const string DLL_PATH_PATTERN_ASSETS_MACRO = "{assets}";
        public const string DLL_PATH_PATTERN_PROJECT_MACRO = "{proj}";
        private const string CRASH_FILE_NAME_PREFIX = "unityNativeCrash_";
        public static readonly Type[] SUPPORTED_PARAMATER_ATTRIBUTES = { typeof(MarshalAsAttribute), typeof(InAttribute), typeof(OutAttribute) };

        public static DllManipulatorOptions Options { get; set; }
        private static int _unityMainThreadId;
        private static string _assetsPath;
        private static readonly ReaderWriterLockSlim _nativeFunctionLoadLock = new ReaderWriterLockSlim();
        private static ModuleBuilder _customDelegateTypesModule = null;
        private static readonly Dictionary<string, NativeDll> _dlls = new Dictionary<string, NativeDll>();
        private static readonly Dictionary<MethodInfo, DynamicMethod> _nativeFunctionMocks = new Dictionary<MethodInfo, DynamicMethod>();
        private static readonly Dictionary<NativeFunctionSignature, Type> _delegateTypesForNativeFunctionSignatures = new Dictionary<NativeFunctionSignature, Type>();
        private static NativeFunction[] _nativeFunctions = null;
        private static int _nativeFunctionsCount = 0;
        private static int _createdDelegateTypes = 0;
        private static int _lastNativeCallIndex = 0; //Use with synchronization


        public static void SetUnityContext(int unityMainThreadId, string assetsPath)
        {
            DllManipulator._unityMainThreadId = unityMainThreadId;
            DllManipulator._assetsPath = assetsPath;
        }

        /// <summary>
        /// Loads all DLLs and functions for mocked methods
        /// </summary>
        public static void LoadAll()
        {
            _nativeFunctionLoadLock.EnterWriteLock(); //Locking with no thread safety option is not required but is ok (this function isn't performance critical)
            try
            {
                foreach (var dll in _dlls.Values)
                {
                    if (dll.handle == IntPtr.Zero)
                    {
                        foreach (var nativeFunction in dll.functions)
                        {
                            LoadTargetFunction(nativeFunction);
                        }
                    }
                }
            }
            finally
            {
                _nativeFunctionLoadLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Unloads all DLLs and functions currently loaded
        /// </summary>
        public static void UnloadAll()
        {
            _nativeFunctionLoadLock.EnterWriteLock(); //Locking with no thread safety option is not required but is ok (this function isn't performance critical)
            try
            {
                foreach (var dll in _dlls.Values)
                {
                    if (dll.handle != IntPtr.Zero)
                    {
                        bool success = SysUnloadDll(dll.handle);
                        dll.handle = IntPtr.Zero;

                        //Reset error states at unload
                        dll.loadingError = false;
                        dll.symbolError = false;

                        foreach (var func in dll.functions)
                        {
                            func.@delegate = null;
                        }

                        if (!success)
                            Debug.LogWarning($"Error while unloading DLL \"{dll.name}\" at path \"{dll.path}\"");
                    }
                }
            }
            finally
            {
                _nativeFunctionLoadLock.ExitWriteLock();
            }
        }

        public static void ForgetAllDlls()
        {
            _dlls.Clear();
            _nativeFunctions = null;
            _nativeFunctionsCount = 0;
        }

        public static void ClearCrashLogs()
        {
            if (Options.enableCrashLogs)
            {
                if (Options.crashLogsDir == null)
                    return;
                var dir = ApplyDirectoryPathMacros(Options.crashLogsDir);
                foreach (var filePath in Directory.GetFiles(dir))
                {
                    if (Path.GetFileName(filePath).StartsWith(CRASH_FILE_NAME_PREFIX))
                        File.Delete(filePath);
                }
            }
        }

        /// <summary>
        /// Creates information snapshot of all known DLLs. 
        /// </summary>
        public static IList<NativeDllInfo> GetUsedDllsInfos()
        {
            var dllInfos = new NativeDllInfo[_dlls.Count];
            int i = 0;
            foreach (var dll in _dlls.Values)
            {
                var loadedFunctions = dll.functions.Select(f => f.identity.symbol).ToList();
                dllInfos[i] = new NativeDllInfo(dll.name, dll.path, dll.handle != IntPtr.Zero, dll.loadingError, dll.symbolError, loadedFunctions);
                i++;
            }

            return dllInfos;
        }

        public static IEnumerable<MethodInfo> FindNativeFunctionsToMock(Assembly assembly)
        {
            var allTypes = assembly.GetTypes();
            foreach (var type in allTypes)
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.IsDefined(typeof(DllImportAttribute)))
                    {
                        if (method.IsDefined(typeof(DisableMockingAttribute)))
                            continue;

                        if (method.DeclaringType.IsDefined(typeof(DisableMockingAttribute)))
                            continue;

                        if (Options.mockAllNativeFunctions || method.IsDefined(typeof(MockNativeDeclarationAttribute)) || method.DeclaringType.IsDefined(typeof(MockNativeDeclarationsAttribute)))
                            yield return method;
                    }
                }
            }
        }

        public static string ApplyDirectoryPathMacros(string path)
        {
            return path
                .Replace(DLL_PATH_PATTERN_ASSETS_MACRO, _assetsPath)
                .Replace(DLL_PATH_PATTERN_PROJECT_MACRO, _assetsPath + "/../");
        }

        public static void MockNativeFunction(MethodInfo function)
        {
            var methodMock = GetNativeFunctionMockMethod(function);
            Memory.MarkForNoInlining(function);
            PrepareDynamicMethod(methodMock);
            Memory.DetourMethod(function, methodMock);
        }

        /// <summary>
        /// Creates and registers new DynamicMethod that mocks <paramref name="nativeMethod"/> and itself calls dynamically loaded function from DLL.
        /// </summary>
        private static DynamicMethod GetNativeFunctionMockMethod(MethodInfo nativeMethod)
        {
            if (!_nativeFunctionMocks.TryGetValue(nativeMethod, out var mockedDynamicMethod))
            {
                var dllImportAttr = nativeMethod.GetCustomAttribute<DllImportAttribute>();
                var dllName = dllImportAttr.Value;
                string dllPath;
                var nativeFunctionSymbol = dllImportAttr.EntryPoint;

                if (_dlls.TryGetValue(dllName, out var dll))
                {
                    dllPath = dll.path;
                }
                else
                {
                    dllPath = ApplyDirectoryPathMacros(Options.dllPathPattern).Replace(DLL_PATH_PATTERN_DLL_NAME_MACRO, dllName);
                    dll = new NativeDll(dllName, dllPath);
                    _dlls.Add(dllName, dll);
                }

                var nativeFunction = new NativeFunction(new NativeFunctionIdentity(nativeFunctionSymbol, dllName), dll);
                dll.functions.Add(nativeFunction);
                var nativeFunctionIndex = _nativeFunctionsCount;
                AddNativeFunction(nativeFunction);
                nativeFunction.index = nativeFunctionIndex;

                var parameters = nativeMethod.GetParameters();
                var parameterTypes = parameters.Select(x => x.ParameterType).ToArray();
                var nativeMethodSignature = new NativeFunctionSignature(nativeMethod, dllImportAttr.CallingConvention,
                    dllImportAttr.BestFitMapping, dllImportAttr.CharSet, dllImportAttr.SetLastError, dllImportAttr.ThrowOnUnmappableChar);
                if (!_delegateTypesForNativeFunctionSignatures.TryGetValue(nativeMethodSignature, out nativeFunction.delegateType))
                {
                    nativeFunction.delegateType = CreateDelegateTypeForNativeFunctionSignature(nativeMethodSignature);
                    _delegateTypesForNativeFunctionSignatures.Add(nativeMethodSignature, nativeFunction.delegateType);
                }
                var targetDelegateInvokeMethod = nativeFunction.delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);

                mockedDynamicMethod = new DynamicMethod(dllName + ":::" + nativeFunctionSymbol, nativeMethod.ReturnType, parameterTypes, typeof(DllManipulator));
                mockedDynamicMethod.DefineParameter(0, nativeMethod.ReturnParameter.Attributes, null);
                for (int i = 0; i < parameters.Length; i++)
                {
                    mockedDynamicMethod.DefineParameter(i + 1, parameters[i].Attributes, null);
                }

                GenerateNativeFunctionMockBody(mockedDynamicMethod.GetILGenerator(), parameters, targetDelegateInvokeMethod, nativeFunctionIndex);
            }

            return mockedDynamicMethod;
        }

        private static void GenerateNativeFunctionMockBody(ILGenerator il, ParameterInfo[] parameters, MethodInfo delegateInvokeMethod, int nativeFunctionIndex)
        {
            var returnsVoid = delegateInvokeMethod.ReturnType == typeof(void);

            if (Options.threadSafe)
            {
                if (!returnsVoid)
                    il.DeclareLocal(delegateInvokeMethod.ReturnType); //Local 0: return value

                il.Emit(OpCodes.Ldsfld, Field_NativeFunctionLoadLock.Value);
                il.Emit(OpCodes.Call, Method_Rwls_EnterReadLock.Value);
                il.BeginExceptionBlock(); //Start lock clause: lock, try {  ...  }, finally { release }
            }

            il.Emit(OpCodes.Ldsfld, Field_NativeFunctions.Value); //Load NativeFunction object
            il.EmitFastI4Load(nativeFunctionIndex);
            il.Emit(OpCodes.Ldelem_Ref);

            if (Options.loadingMode == DllLoadingMode.Lazy) //If lazy mode, load the function. Otherwise we assume it's already loaded
            {
                if (Options.threadSafe)
                    throw new InvalidOperationException("Thread safety with Lazy mode is not supported");

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Call, Method_LoadTargetFunction.Value);
            }

            if (Options.enableCrashLogs) //Log function invocation
            {
                il.EmitFastI4Load(parameters.Length); //Generate array of arguments
                il.Emit(OpCodes.Newarr, typeof(object));
                for (int i = 0; i < parameters.Length; i++)
                {
                    il.Emit(OpCodes.Dup);
                    il.EmitFastI4Load(i);
                    il.EmitFastArgLoad(i);
                    if (parameters[i].ParameterType.IsValueType)
                        il.Emit(OpCodes.Box, parameters[i].ParameterType);
                    il.Emit(OpCodes.Stelem_Ref);
                }
                il.Emit(OpCodes.Call, Method_WriteNativeCrashLog.Value);

                il.Emit(OpCodes.Ldsfld, Field_NativeFunctions.Value); //Once again load native function, previous one was consumed by log method
                il.EmitFastI4Load(nativeFunctionIndex);
                il.Emit(OpCodes.Ldelem_Ref);
            }

            il.Emit(OpCodes.Ldfld, Field_NativeFunctionDelegate.Value);
            //Seems like cast to concrete delegate type is not required here
            for (int i = 0; i < parameters.Length; i++)
            {
                il.EmitFastArgLoad(i);
            }
            //il.Emit(OpCodes.Callvirt, delegateInvokeMethod); //Call native function
            EmitNativeFunctionCall(il, delegateInvokeMethod, parameters.Length);

            if (Options.threadSafe) //End lock clause. Lock is being held during execution of native function, which is necessary since the DLL could be otherwise unloaded between acquire of delegate and call to delegate
            {
                var retLabel = il.DefineLabel();
                if (!returnsVoid)
                    il.Emit(OpCodes.Stloc_0);
                il.Emit(OpCodes.Leave_S, retLabel);
                il.BeginFinallyBlock();
                il.Emit(OpCodes.Ldsfld, Field_NativeFunctionLoadLock.Value);
                il.Emit(OpCodes.Call, Method_Rwls_ExitReadLock.Value);
                il.EndExceptionBlock();
                il.MarkLabel(retLabel);
                if (!returnsVoid)
                    il.Emit(OpCodes.Ldloc_0);
            }
            il.Emit(OpCodes.Ret);
        }


        /// <summary>
        /// Used to bypass mono bug https://github.com/mono/mono/issues/16570
        /// Based on https://github.com/mono/mono/blob/82e573122a55482bf6592f36f819597238628385/mcs/class/corlib/System.Reflection.Emit/ILGenerator.cs#L748
        /// </summary>
        private static void EmitNativeFunctionCall(ILGenerator il, MethodInfo func, int parameterCount)
        {
            var opcode = OpCodes.Callvirt;

            var tokenGenerator = Field_IlGenerator_token_gen.Value.GetValue(il);
            var h_GetToken = MethodInvoker.GetHandler(Method_TokenGenerator_GetToken.Value);
            int token = (int)h_GetToken(tokenGenerator, new object[] { func, true });

            var h_make_room = MethodInvoker.GetHandler(Method_IlGenerator_make_room.Value);
            h_make_room(il, new object[] { 6 });

            var h_ll_emit = MethodInvoker.GetHandler(Method_IlGenerator_ll_emit.Value);
            h_ll_emit(il, new object[] { opcode });

            var declaringType = func.DeclaringType;
            if (declaringType != null)
            {
                if (Type_MethodOnTypeBuilderInst.Value.IsAssignableFrom(func.GetType()) || func is MethodBuilder)
                {
                    var h_add_token_fixup = MethodInvoker.GetHandler(Method_IlGenerator_add_token_fixup.Value);
                    h_add_token_fixup(il, new object[] { func });
                }
            }

            var h_emit_int = MethodInvoker.GetHandler(Method_IlGenerator_emit_int.Value);
            h_emit_int(il, new object[] { token });

            int stack = (int)Field_IlGenerator_cur_stack.Value.GetValue(il);

            if (func.ReturnType != typeof(void))
                stack++;

            if (opcode.StackBehaviourPop == StackBehaviour.Varpop)
                stack -= parameterCount;

            Field_IlGenerator_cur_stack.Value.SetValue(il, stack);
        }

        /// <summary>
        /// Prepares <paramref name="method"/> to be injected (aka. patched) into other method
        /// </summary>
        private static void PrepareDynamicMethod(DynamicMethod method)
        {
            //
            // This method is logically copy of DynamicTools.PrepareDynamicMethod(DynamicMethod method) from https://github.com/pardeike/Harmony
            //

            if (Method_DynamicMethod_CreateDynMethod.Value != null)
            {
                var h_CreateDynMethod = MethodInvoker.GetHandler(Method_DynamicMethod_CreateDynMethod.Value);
                h_CreateDynMethod(method, new object[0]);
            }
            else
            {
                throw new Exception("DynamicMethod.CreateDynMethod not found");
            }
        }

        /// <summary>
        /// Adds <paramref name="nativeFunction"/> to <see cref="_nativeFunctions"/> list
        /// </summary>
        private static void AddNativeFunction(NativeFunction nativeFunction)
        {
            if (_nativeFunctions == null)
                _nativeFunctions = new NativeFunction[4];

            if (_nativeFunctionsCount == _nativeFunctions.Length)
            {
                var newArray = new NativeFunction[_nativeFunctions.Length * 2];
                Array.Copy(_nativeFunctions, newArray, _nativeFunctions.Length);
                _nativeFunctions = newArray;
            }

            _nativeFunctions[_nativeFunctionsCount++] = nativeFunction;
        }

        private static Type CreateDelegateTypeForNativeFunctionSignature(NativeFunctionSignature functionSignature)
        {
            if (_customDelegateTypesModule == null)
            {
                var aName = new AssemblyName("HelperRuntimeDelegates");
                var delegateTypesAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndSave);
                _customDelegateTypesModule = delegateTypesAssembly.DefineDynamicModule(aName.Name, aName.Name + ".dll");
            }

            var delBuilder = _customDelegateTypesModule.DefineType("HelperNativeDelegate" + _createdDelegateTypes.ToString(),
                TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass, typeof(MulticastDelegate));

            //ufp = UnmanagedFunctionPointer
            object[] ufpAttrCtorArgValues = { functionSignature.callingConvention };
            FieldInfo[] ufpAttrNamedFields = { Field_Ufpa_BestFitMapping.Value,  Field_Ufpa_CharSet.Value,  Field_Ufpa_SetLastError.Value,  Field_Ufpa_ThrowOnUnmappableChar.Value  };
            object[] ufpAttrFieldValues =    { functionSignature.bestFitMapping, functionSignature.charSet, functionSignature.setLastError, functionSignature.throwOnUnmappableChar };
            var ufpAttrBuilder = new CustomAttributeBuilder(Ctor_Ufp.Value, ufpAttrCtorArgValues, ufpAttrNamedFields, ufpAttrFieldValues);
            delBuilder.SetCustomAttribute(ufpAttrBuilder);

            var ctorBuilder = delBuilder.DefineConstructor(MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard, DELEGATE_CTOR_PARAMETERS);
            ctorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var invokeBuilder = delBuilder.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot,
                CallingConventions.Standard | CallingConventions.HasThis, functionSignature.returnParameter.type, functionSignature.parameters.Select(p => p.type).ToArray());
            invokeBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
            var invokeReturnParam = invokeBuilder.DefineParameter(0, functionSignature.returnParameter.parameterAttributes, null);
            foreach (var attr in functionSignature.returnParameter.customAttributes)
            {
                invokeReturnParam.SetCustomAttribute(CreateMarshalingAttributeBuilderFromAttributeInstance(attr));
            }
            for (int i = 0; i < functionSignature.parameters.Length; i++)
            {
                var param = functionSignature.parameters[i];
                var paramBuilder = invokeBuilder.DefineParameter(i + 1, param.parameterAttributes, null);
                foreach (var attr in param.customAttributes)
                {
                    paramBuilder.SetCustomAttribute(CreateMarshalingAttributeBuilderFromAttributeInstance(attr));
                }
            }

            _createdDelegateTypes++;
            return delBuilder.CreateType();
        }

        private static CustomAttributeBuilder CreateMarshalingAttributeBuilderFromAttributeInstance(Attribute attribute)
        {
            var attrType = attribute.GetType();
            switch (attribute)
            {
                case MarshalAsAttribute marshalAsAttribute:
                {
                    var ctor = attrType.GetConstructor(MARSHAL_AS_ATTRIBUTE_CTOR_PARAMETERS);
                    object[] ctorArgs = { marshalAsAttribute.Value };
                    var fields = attrType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                            .Where(f => f.FieldType.IsValueType).ToArray(); //XXX: Used to bypass Mono bug, see https://github.com/mono/mono/issues/12747
                    var fieldArgumentValues = new object[fields.Length];
                    for (int i = 0; i < fields.Length; i++)
                    {
                        fieldArgumentValues[i] = fields[i].GetValue(attribute);
                    }

                    //MarshalAsAttribute has no properties other than Value, which is passed in constructor, hence empty properties array
                    return new CustomAttributeBuilder(ctor, ctorArgs, Array.Empty<PropertyInfo>(), Array.Empty<object>(),
                        fields, fieldArgumentValues);
                }
                case InAttribute _:
                {
                    var ctor = attrType.GetConstructor(Type.EmptyTypes);
                    return new CustomAttributeBuilder(ctor, Array.Empty<object>(), Array.Empty<PropertyInfo>(), Array.Empty<object>(),
                        Array.Empty<FieldInfo>(), Array.Empty<object>());
                }
                case OutAttribute _:
                {
                    var ctor = attrType.GetConstructor(Type.EmptyTypes);
                    return new CustomAttributeBuilder(ctor, Array.Empty<object>(), Array.Empty<PropertyInfo>(), Array.Empty<object>(),
                        Array.Empty<FieldInfo>(), Array.Empty<object>());
                }
                default:
                    throw new NotImplementedException($"Attribute {attrType} is not supported");
            }
        }

        /// <summary>
        /// Loads DLL and function delegate of <paramref name="nativeFunction"/> if not yet loaded.
        /// To achieve thread safety calls to this method must be synchronized.
        /// Note: This method is being called by dynamically generated code. Be careful when changing its signature.
        /// </summary>
        private static void LoadTargetFunction(NativeFunction nativeFunction)
        {
            var dll = nativeFunction.containingDll;
            if (dll.handle == IntPtr.Zero)
            {
                dll.handle = SysLoadDll(dll.path);
                if (dll.handle == IntPtr.Zero)
                {
                    dll.loadingError = true;
                    throw new NativeDllException($"Could not load DLL \"{dll.name}\" at path \"{dll.path}\".");
                }
            }

            if (nativeFunction.@delegate == null)
            {
                IntPtr funcPtr = SysGetDllProcAddress(dll.handle, nativeFunction.identity.symbol);
                if (funcPtr == IntPtr.Zero)
                {
                    dll.symbolError = true;
                    throw new NativeDllException($"Could not get address of symbol \"{nativeFunction.identity.symbol}\" in DLL \"{dll.name}\" at path \"{dll.path}\".");
                }

                nativeFunction.@delegate = Marshal.GetDelegateForFunctionPointer(funcPtr, nativeFunction.delegateType);
            }
        }

        /// <summary>
        /// Logs native function's call to file. If that file exists, it is overwritten. One file is maintained for each thread.
        /// Note: This method is being called by dynamically generated code. Be careful when changing its signature.
        /// </summary>
        private static void WriteNativeCrashLog(NativeFunction nativeFunction, object[] arguments)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var filePath = Path.Combine(ApplyDirectoryPathMacros(Options.crashLogsDir), $"{CRASH_FILE_NAME_PREFIX}tid{threadId}.log");
            using (var file = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)) //Truncates file if exists
            {
                using (var writer = new StreamWriter(file))
                {
                    writer.Write("function: ");
                    writer.WriteLine(nativeFunction.identity.symbol);

                    writer.Write($"from DLL: ");
                    writer.WriteLine(nativeFunction.containingDll.name);

                    writer.Write($"  at path: ");
                    writer.WriteLine(nativeFunction.containingDll.path);

                    writer.Write("arguments: ");
                    if (arguments.Length == 0)
                    {
                        writer.WriteLine("no arguments");
                    }
                    else
                    {
                        writer.WriteLine();
                        for (int i = 0; i < arguments.Length; i++)
                        {
                            writer.Write($"  {i}:".PadRight(5));
                            var param = arguments[i];
                            if (param == null)
                            {
                                writer.Write("null");
                            }
                            else
                            {
                                switch (param)
                                {
                                    case string _:
                                        writer.Write($"\"{param}\"");
                                        break;
                                    //For float types use InvariantCulture, as so to use dot decimal separator over comma
                                    case float f:
                                        writer.Write(f.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        break;
                                    case double f:
                                        writer.Write(f.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        break;
                                    case decimal f:
                                        writer.Write(f.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        break;
                                    default:
                                        writer.Write(param);
                                        break;
                                }
                            }
                            writer.WriteLine();
                        }
                    }

                    writer.Write("thread: ");
                    if (threadId == _unityMainThreadId)
                        writer.WriteLine("unity main thread");
                    else
                        writer.WriteLine($"{Thread.CurrentThread.Name}({threadId})");

                    var nativeCallIndex = Interlocked.Increment(ref _lastNativeCallIndex) - 1;
                    writer.Write("call index: ");
                    writer.WriteLine(nativeCallIndex);

                    if (Options.crashLogsStackTrace)
                    {
                        var stackTrace = new System.Diagnostics.StackTrace(1); //Skip this frame
                        writer.WriteLine("stack trace:");
                        writer.Write(stackTrace.ToString());
                    }
                }
            }
        }

        private static IntPtr SysLoadDll(string filepath)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes_Windows.LoadLibrary(filepath);
#elif UNITY_STANDALONE_LINUX
            return PInvokes_Linux.dlopen(filepath, (int)Options.unixDlopenFlags);
#elif UNITY_STANDALONE_OSX
            return PInvokes_Osx.dlopen(filepath, (int)Options.unixDlopenFlags);
#endif
        }

        private static bool SysUnloadDll(IntPtr libHandle)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes_Windows.FreeLibrary(libHandle);
#elif UNITY_STANDALONE_LINUX
            return PInvokes_Linux.dlclose(libHandle) == 0;
#elif UNITY_STANDALONE_OSX
            return PInvokes_Osx.dlclose(libHandle) == 0;
#endif
        }

        private static IntPtr SysGetDllProcAddress(IntPtr libHandle, string symbol)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes_Windows.GetProcAddress(libHandle, symbol);
#elif UNITY_STANDALONE_LINUX
            return PInvokes_Linux.dlsym(libHandle, symbol);
#elif UNITY_STANDALONE_OSX
            return PInvokes_Osx.dlsym(libHandle, symbol);
#endif
        }
    }

    [Serializable]
    public class DllManipulatorOptions
    {
        public string dllPathPattern;
        public string[] assemblyPaths; //empty means only executing assembly
        public DllLoadingMode loadingMode;
        public Unix_DlopenFlags unixDlopenFlags;
        public bool threadSafe;
        public bool enableCrashLogs;
        public string crashLogsDir;
        public bool crashLogsStackTrace;
        public bool mockAllNativeFunctions;
        public bool onlyInEditor;
    }

    public enum DllLoadingMode
    {
        Lazy,
        Preload
    }
}