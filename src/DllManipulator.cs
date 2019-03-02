using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Harmony;
using DllManipulator.Internal;
using System.IO;

namespace DllManipulator
{
    //Note: "DLL" used in this code refers to Dynamically Loaded Library, and not to the .dll file extension on Windows.
    public partial class DllManipulator : MonoBehaviour
    {
        public const string DLL_PATH_PATTERN_DLL_NAME_MACRO = "{name}";
        public const string DLL_PATH_PATTERN_ASSETS_MACRO = "{assets}";
        public const string DLL_PATH_PATTERN_PROJECT_MACRO = "{proj}";
        private const string CRASH_FILE_NAME_PREFIX = "unityNativeCrash_";
        public static readonly Type[] SUPPORTED_PARAMATER_ATTRIBUTES = { typeof(MarshalAsAttribute), typeof(InAttribute), typeof(OutAttribute) };
        private static readonly Type[] DELEGATE_CTOR_PARAMETERS = { typeof(object), typeof(IntPtr) };
        private static readonly Type[] UNMANAGED_FUNCTION_POINTER_ATTRIBUTE_CTOR_PARAMETERS = { typeof(CallingConvention) };
        private static readonly Type[] MARSHAL_AS_ATTRIBUTE_CTOR_PARAMETERS = { typeof(UnmanagedType) };

        public DllManipulatorOptions Options = new DllManipulatorOptions()
        {
#if UNITY_STANDALONE_WIN
            dllPathPattern = "{assets}/Plugins/__{name}.dll",
#elif UNITY_STANDALONE_LINUX
            dllPathPattern = "{assets}/Plugins/__{name}.so",
#elif UNITY_STANDALONE_OSX
            dllPathPattern = "{assets}/Plugins/__{name}.dylib",
#endif
            loadingMode = DllLoadingMode.Lazy,
            unixDlopenFlags = UnixDlopenFlags.Lazy,
            threadSafe = false,
            crashLogs = false,
            crashLogsDir = "{assets}/",
            crashLogsStackTrace = false,
            mockAllNativeFunctions = false,
            mockCallsInAllTypes = false,
        };

        public static TimeSpan? InitializationTime { get; private set; } = null;
        private static DllManipulatorOptions _options;
        private static DllManipulator _singletonInstance = null;
        private static int _unityMainThreadId;
        private static string _assetsPath;
        private static readonly ReaderWriterLockSlim _nativeFunctionLoadLock = new ReaderWriterLockSlim();
        private static ModuleBuilder _customDelegateTypesModule = null;
        private static readonly Dictionary<string, NativeDll> _dlls = new Dictionary<string, NativeDll>();
        private static readonly HashSet<MethodInfo> _nativeFunctionsToMock = new HashSet<MethodInfo>();
        private static readonly Dictionary<MethodInfo, DynamicMethod> _nativeCallMocks = new Dictionary<MethodInfo, DynamicMethod>();
        private static readonly Dictionary<NativeFunctionSignature, Type> _delegateTypesForNativeFunctionSignatures = new Dictionary<NativeFunctionSignature, Type>();
        private static NativeFunction[] _nativeFunctions = null;
        private static int _nativeFunctionsCount = 0;
        private static int _createdDelegateTypes = 0;
        private static int _lastNativeCallIndex = 0; //Use with synchronization


        private void OnEnable()
        {
            if (_singletonInstance != null)
            {
                if (_singletonInstance != this)
                {
                    Destroy(gameObject);
                }

                return;
            }
            _singletonInstance = this;

            _unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
            _assetsPath = Application.dataPath;

            DontDestroyOnLoad(gameObject);

            _options = Options;
            Initialize();
        }

        private void OnApplicationQuit()
        {
            UnloadAll();
            ForgetAllDlls();
            ClearCrashLogs();
        }


        private static void Initialize()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var allTypes = Assembly.GetExecutingAssembly().GetTypes();

            foreach (var type in allTypes)
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.IsDefined(typeof(DllImportAttribute)))
                    {
                        if (!method.IsDefined(typeof(DisableMockingAttribute)) && _options.mockAllNativeFunctions || method.IsDefined(typeof(MockNativeDeclarationAttribute)) || method.DeclaringType.IsDefined(typeof(MockNativeDeclarationsAttribute)))
                        {
                            _nativeFunctionsToMock.Add(method);
                        }
                    }
                }
            }

            if (_nativeFunctionsToMock.Count == 0)
            {
                Debug.LogWarning($"Didn't find any native functions to mock.");
                return;
            }

            var harmony = HarmonyInstance.Create(nameof(DllManipulator));
            var callingMethodTranspiler = new HarmonyMethod(typeof(DllManipulator).GetMethod(nameof(CallingMethodTranspiler), BindingFlags.Static | BindingFlags.NonPublic));

            int mockedNativeFunctionCalls = 0;
            foreach (var type in allTypes)
            {
                if (_options.mockCallsInAllTypes || type.IsDefined(typeof(MockNativeCallsAttribute)))
                {
                    foreach (var method in type.GetRuntimeMethods().Cast<MethodBase>()
                        .Concat(type.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
                    {
                        if (!(method is DynamicMethod) && method.DeclaringType == type && method.GetMethodBody() != null)
                        {
                            harmony.Patch(method, transpiler: callingMethodTranspiler);
                            mockedNativeFunctionCalls++;
                        }
                    }
                }
            }

            if(mockedNativeFunctionCalls == 0)
            {
                Debug.LogWarning($"Found native method(s) to mock, but no call to any.");
                return;
            }

            if(_options.loadingMode == DllLoadingMode.Preload)
            {
                LoadAll();
            }

            timer.Stop();
            InitializationTime = timer.Elapsed;
        }

        public static void LoadAll()
        {
            _nativeFunctionLoadLock.EnterWriteLock(); //Locking with no thread safety option is not required but this function isn't performance critical
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

        public static void UnloadAll()
        {
            _nativeFunctionLoadLock.EnterWriteLock(); //Locking with no thread safety option is not required but this function isn't performance critical
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
                        {
                            Debug.LogWarning($"Error while unloading DLL \"{dll.name}\" at path \"{dll.path}\"");
                        }
                    }
                }
            }
            finally
            {
                _nativeFunctionLoadLock.ExitWriteLock();
            }
        }

        private static void ForgetAllDlls()
        {
            _dlls.Clear();
            _nativeFunctions = null;
            _nativeFunctionsCount = 0;
        }

        private static void ClearCrashLogs()
        {
            if (_options.crashLogs)
            {
                var dir = ApplyDirectoryPathMacros(_options.crashLogsDir);
                foreach (var filePath in Directory.GetFiles(dir))
                {
                    if (Path.GetFileName(filePath).StartsWith(CRASH_FILE_NAME_PREFIX))
                    {
                        File.Delete(filePath);
                    }
                }
            }
        }

        private static string ApplyDirectoryPathMacros(string path)
        {
            return path
                .Replace(DLL_PATH_PATTERN_ASSETS_MACRO, _assetsPath)
                .Replace(DLL_PATH_PATTERN_PROJECT_MACRO, _assetsPath + "/../");
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

        /// <summary>
        /// Transplits methods that may be calling native functions (extern methods with [ImportDll] attribute) by replacing that call with new, dynamic method.
        /// </summary>
        private static IEnumerable<CodeInstruction> CallingMethodTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Call)
                {
                    if (instr.operand is MethodInfo nativeMethod && _nativeFunctionsToMock.Contains(nativeMethod))
                    {
                        if (!_nativeCallMocks.TryGetValue(nativeMethod, out var newMethod))
                        {
                            newMethod = CreateNewNativeFunctionMock(nativeMethod);
                            _nativeCallMocks.Add(nativeMethod, newMethod);
                        }

                        yield return new CodeInstruction(OpCodes.Call, newMethod);
                    }
                    else
                    {
                        yield return instr;
                    }
                }
                else
                {
                    yield return instr;
                }
            }
        }

        /// <summary>
        /// Creates and registers new DynamicMethod that mocks <paramref name="nativeMethod"/> and itself calls dynamically loaded function from DLL.
        /// </summary>
        private static DynamicMethod CreateNewNativeFunctionMock(MethodInfo nativeMethod)
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
                dllPath = ApplyDirectoryPathMacros(_options.dllPathPattern).Replace(DLL_PATH_PATTERN_DLL_NAME_MACRO, dllName);
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

            var mockedDynamicMethod = new DynamicMethod(dllName + ":::" + nativeFunctionSymbol, nativeMethod.ReturnType, parameterTypes, typeof(DllManipulator));
            mockedDynamicMethod.DefineParameter(0, nativeMethod.ReturnParameter.Attributes, null);
            for (int i = 0; i < parameters.Length; i++)
            {
                mockedDynamicMethod.DefineParameter(i + 1, parameters[i].Attributes, null);
            }

            GenerateNativeFunctionMockBody(mockedDynamicMethod.GetILGenerator(), parameters.Length, parameterTypes, targetDelegateInvokeMethod, nativeFunctionIndex);

            return mockedDynamicMethod;
        }

        /// <summary>
        /// Adds <paramref name="nativeFunction"/> to <see cref="_nativeFunctions"/> list
        /// </summary>
        private static void AddNativeFunction(NativeFunction nativeFunction)
        {
            if (_nativeFunctions == null)
            {
                _nativeFunctions = new NativeFunction[4];
            }

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
            var ufpAttrType = typeof(UnmanagedFunctionPointerAttribute);
            var ufpAttrCtor = ufpAttrType.GetConstructor(UNMANAGED_FUNCTION_POINTER_ATTRIBUTE_CTOR_PARAMETERS);
            object[] ufpAttrCtorArgValues = { functionSignature.callingConvention };
            FieldInfo[] ufpAttrNamedFields = {
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.BestFitMapping), BindingFlags.Public | BindingFlags.Instance),
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.CharSet), BindingFlags.Public | BindingFlags.Instance),
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.SetLastError), BindingFlags.Public | BindingFlags.Instance),
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.ThrowOnUnmappableChar), BindingFlags.Public | BindingFlags.Instance),
            };
            object[] ufpAttrFieldValues = { functionSignature.bestFitMapping, functionSignature.charSet, functionSignature.setLastError, functionSignature.throwOnUnmappableChar };
            var ufpAttrBuilder = new CustomAttributeBuilder(ufpAttrCtor, ufpAttrCtorArgValues, ufpAttrNamedFields, ufpAttrFieldValues);
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
                invokeReturnParam.SetCustomAttribute(GetAttributeBuilderFromAttributeInstance(attr));
            }
            for (int i = 0; i < functionSignature.parameters.Length; i++)
            {
                var param = functionSignature.parameters[i];
                var paramBuilder = invokeBuilder.DefineParameter(i + 1, param.parameterAttributes, null);
                foreach(var attr in param.customAttributes)
                {
                    paramBuilder.SetCustomAttribute(GetAttributeBuilderFromAttributeInstance(attr));
                }
            }

            _createdDelegateTypes++;
            return delBuilder.CreateType();
        }

        private static CustomAttributeBuilder GetAttributeBuilderFromAttributeInstance(Attribute attribute)
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
                    for(int i = 0; i < fields.Length; i++)
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

        private static void GenerateNativeFunctionMockBody(ILGenerator il, int parameterCount, Type[] parameterTypes, MethodInfo delegateInvokeMethod, int nativeFunctionIndex)
        {
            var returnsVoid = delegateInvokeMethod.ReturnType == typeof(void);

            if (_options.threadSafe)
            {
                if (!returnsVoid)
                {
                    il.DeclareLocal(delegateInvokeMethod.ReturnType); //Local 0: returnValue
                }

                il.Emit(OpCodes.Ldsfld, NativeFunctionLoadLockField.Value);
                il.Emit(OpCodes.Call, RwlsEnterReadLocKMethod.Value);
                il.BeginExceptionBlock();
            }

            il.Emit(OpCodes.Ldsfld, NativeFunctionsField.Value);
            il.EmitFastI4Load(nativeFunctionIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            if (_options.loadingMode == DllLoadingMode.Lazy)
            {
                if (_options.threadSafe)
                {
                    throw new InvalidOperationException("Thread safety with Lazy mode is not supported");
                }

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Call, LoadTargetFunctionMethod.Value);
            }
            if(_options.crashLogs)
            {
                il.EmitFastI4Load(parameterCount); //Generate array of arguments
                il.Emit(OpCodes.Newarr, typeof(object));
                for (int i = 0; i < parameterCount; i++)
                {
                    il.Emit(OpCodes.Dup);
                    il.EmitFastI4Load(i);
                    il.EmitFastArgLoad(i);
                    if(parameterTypes[i].IsValueType)
                    {
                        il.Emit(OpCodes.Box, parameterTypes[i]);
                    }
                    il.Emit(OpCodes.Stelem_Ref);
                }
                il.Emit(OpCodes.Call, WriteNativeCrashLogMethod.Value);

                il.Emit(OpCodes.Ldsfld, NativeFunctionsField.Value); //Once again load native function (previous one was consumed by log method)
                il.EmitFastI4Load(nativeFunctionIndex);
                il.Emit(OpCodes.Ldelem_Ref);
            }
            il.Emit(OpCodes.Ldfld, NativeFunctionDelegateField.Value);
            //Seems like cast to concrete delegate type is not required here

            for (int i = 0; i < parameterCount; i++)
            {
                il.EmitFastArgLoad(i);
            }
            il.Emit(OpCodes.Callvirt, delegateInvokeMethod);

            if (_options.threadSafe) //End lock clause. Lock is being held during execution of native function, which is necessary since the DLL could be otherwise unloaded between acquire of delegate and call to delegate
            {
                var retLabel = il.DefineLabel();
                if (!returnsVoid)
                {
                    il.Emit(OpCodes.Stloc_0);
                }
                il.Emit(OpCodes.Leave_S, retLabel);
                il.BeginFinallyBlock();
                il.Emit(OpCodes.Ldsfld, NativeFunctionLoadLockField.Value);
                il.Emit(OpCodes.Call, RwlsExitReadLockMethod.Value);
                il.EndExceptionBlock();
                il.MarkLabel(retLabel);
                if (!returnsVoid)
                {
                    il.Emit(OpCodes.Ldloc_0);
                }
            }
            il.Emit(OpCodes.Ret);
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
            var filePath = Path.Combine(ApplyDirectoryPathMacros(_options.crashLogsDir), $"{CRASH_FILE_NAME_PREFIX}tid{threadId}.log");
            using (var file = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)) //Truncates file if exists
            {
                using(var writer = new StreamWriter(file))
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
                        writer.Write("no arguments");
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
                    if(threadId == _unityMainThreadId)
                    {
                        writer.WriteLine("unity main thread");
                    }
                    else
                    {
                        writer.WriteLine($"{Thread.CurrentThread.Name}({threadId})");
                    }

                    var nativeCallIndex = Interlocked.Increment(ref _lastNativeCallIndex) - 1;
                    writer.Write("call index: ");
                    writer.WriteLine(nativeCallIndex);

                    if (_options.crashLogsStackTrace)
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
            return PInvokes.Windows_LoadLibrary(filepath);
#elif UNITY_STANDALONE_LINUX
            return PInvokes.Linux_dlopen(filepath, (int)_options.unixDlopenFlags);
#elif UNITY_STANDALONE_OSX
            return PInvokes.Osx_dlopen(filepath, (int)_options.unixDlopenFlags);
#endif
        }

        private static bool SysUnloadDll(IntPtr libHandle)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes.Windows_FreeLibrary(libHandle);
#elif UNITY_STANDALONE_LINUX
            return PInvokes.Linux_dlclose(libHandle) == 0;
#elif UNITY_STANDALONE_OSX
            return PInvokes.Osx_dlclose(libHandle) == 0;
#endif
        }

        private static IntPtr SysGetDllProcAddress(IntPtr libHandle, string symbol)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes.Windows_GetProcAddress(libHandle, symbol);
#elif UNITY_STANDALONE_LINUX
            return PInvokes.Linux_dlsym(libHandle, symbol);
#elif UNITY_STANDALONE_OSX
            return PInvokes.Osx_dlsym(libHandle, symbol);
#endif
        }
    }

    [Serializable]
    public class DllManipulatorOptions
    {
        public string dllPathPattern;
        public DllLoadingMode loadingMode;
        public UnixDlopenFlags unixDlopenFlags;
        public bool threadSafe;
        public bool crashLogs;
        public string crashLogsDir;
        public bool crashLogsStackTrace;
        public bool mockAllNativeFunctions;
        public bool mockCallsInAllTypes;
    }

    public enum DllLoadingMode
    {
        Lazy,
        Preload
    }

    public enum UnixDlopenFlags : int
    {
        Lazy = 0x00001,
        Now = 0x00002,
        Lazy_Global = 0x00100 | Lazy,
        Now_Global = 0x00100 | Now
    }
}