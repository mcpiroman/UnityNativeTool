using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UnityEngine;
using Harmony;
using DllManipulator.Internal;

namespace DllManipulator
{
    //Note: "DLL" used in this code refers to Dynamically Loaded Library, and not to the .dll file extension on Windows.
    public class DllManipulator : MonoBehaviour
    {
        public const string DLL_PATH_PATTERN_NAME_MACRO = "{name}";
        public const string DLL_PATH_PATTERN_ASSETS_MACRO = "{assets}";
        public const string DLL_PATH_PATTERN_PROJECT_MACRO = "{proj}";
        private static readonly Type[] DELEGATE_CTOR_PARAMETERS = new[] { typeof(object), typeof(IntPtr) };
        private static readonly Type[] UNMANAGED_FUNCTION_POINTER_ATTRIBUTE_CTOR_PARAMETERS = new[] { typeof(CallingConvention) };

        public DllManipulatorOptions Options = new DllManipulatorOptions()
        {
#if UNITY_STANDALONE_WIN
            dllPathPattern = "{assets}/Plugins/__{name}.dll",
#elif UNITY_STANDALONE_LINUX
            dllPathPattern = "{assets}/Plugins/__{name}.so",
#endif
            loadingMode = DllLoadingMode.Lazy,
            linuxDlopenFlags = LinuxDlopenFlags.Lazy,
            mockAllNativeFunctions = false,
            mockCallsInAllTypes = false,
        };

        private static DllManipulatorOptions _options;
        private static DllManipulator _singletonInstance = null;
        private static MethodInfo _loadTargetFunctionMethod = null;
        private static ModuleBuilder _customDelegateTypesModule = null;
        private static readonly Dictionary<string, NativeDll> _dlls = new Dictionary<string, NativeDll>();
        private static FieldInfo _nativeFunctionDelegateField = null;
        private static readonly HashSet<MethodInfo> _nativeFunctionsToMock = new HashSet<MethodInfo>();
        private static readonly Dictionary<MethodInfo, DynamicMethod> _nativeCallMocks = new Dictionary<MethodInfo, DynamicMethod>();
        private static readonly Dictionary<NativeFunctionSignature, Type> _delegateTypesForNativeFunctionSignatures = new Dictionary<NativeFunctionSignature, Type>();
        private static NativeFunction[] _nativeFunctions = null;
        private static FieldInfo _nativeFunctionsField = null;
        private static int _nativeFunctionsCount = 0;
        private static int _createdDelegateTypes = 0;


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

            DontDestroyOnLoad(gameObject);

            _options = Options;
            Initialize();
        }

        private void OnApplicationQuit()
        {
            UnloadAll();
            ForgetAllDlls();
        }

        private static void Initialize()
        {
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
        }

        public static void LoadAll()
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

        public static void UnloadAll()
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

                    foreach(var func in dll.functions)
                    {
                        func.@delegate = null;
                    }

                    if(!success)
                    {
                        Debug.LogWarning($"Error while unloading DLL \"{dll.name}\" at path \"{dll.path}\"");
                    }
                }
            }
        }

        private static void ForgetAllDlls()
        {
            _dlls.Clear();
            _nativeFunctions = null;
            _nativeFunctionsCount = 0;
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
                dllPath = GetDllPath(dllName);
                dll = new NativeDll(dllName, dllPath);
                _dlls.Add(dllName, dll);
            }

            var nativeFunction = new NativeFunction(new NativeFunctionIdentity(nativeFunctionSymbol, dllName), dll);
            dll.functions.Add(nativeFunction);
            var nativeFunctionIndex = _nativeFunctionsCount;
            AddNativeFunction(nativeFunction);
            nativeFunction.index = nativeFunctionIndex;

            var parameters = nativeMethod.GetParameters();
            var parametersTypes = parameters.Select(x => x.ParameterType).ToArray();
            var nativeMethodSignature = new NativeFunctionSignature(nativeMethod, dllImportAttr.CallingConvention, 
                dllImportAttr.BestFitMapping, dllImportAttr.CharSet, dllImportAttr.SetLastError, dllImportAttr.ThrowOnUnmappableChar);
            if (!_delegateTypesForNativeFunctionSignatures.TryGetValue(nativeMethodSignature, out nativeFunction.delegateType))
            {
                nativeFunction.delegateType = CreateDelegateTypeForNativeFunctionSignature(nativeMethodSignature);
                _delegateTypesForNativeFunctionSignatures.Add(nativeMethodSignature, nativeFunction.delegateType);
            }
            var targetDelegateInvokeMethod = nativeFunction.delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);

            var mockedDynamicMethod = new DynamicMethod(dllName + ":::" + nativeFunctionSymbol, nativeMethod.ReturnType, parametersTypes, typeof(DllManipulator));
            mockedDynamicMethod.DefineParameter(0, nativeMethod.ReturnParameter.Attributes, nativeMethod.ReturnParameter.Name);
            for (int i = 0; i < parameters.Length; i++)
            {
                mockedDynamicMethod.DefineParameter(parameters[i].Position, parameters[i].Attributes, parameters[i].Name);
            }

            if (_nativeFunctionsField == null)
            {
                _nativeFunctionsField = typeof(DllManipulator).GetField(nameof(_nativeFunctions), BindingFlags.NonPublic | BindingFlags.Static);
            }

            if (_nativeFunctionDelegateField == null)
            {
                _nativeFunctionDelegateField = typeof(NativeFunction).GetField(nameof(NativeFunction.@delegate), BindingFlags.Public | BindingFlags.Instance);
            }

            if (_options.loadingMode == DllLoadingMode.Lazy)
            {
                if (_loadTargetFunctionMethod == null)
                {
                    _loadTargetFunctionMethod = typeof(DllManipulator).GetMethod(nameof(LoadTargetFunction), BindingFlags.NonPublic | BindingFlags.Static);
                }
            }

            GenerateNativeFunctionMockBody(mockedDynamicMethod.GetILGenerator(), parameters.Length, targetDelegateInvokeMethod, nativeFunctionIndex);

            return mockedDynamicMethod;
        }

        private static void GenerateNativeFunctionMockBody(ILGenerator il, int parameterCount, MethodInfo delegateInvokeMethod, int nativeFunctionIndex)
        {
            il.Emit(OpCodes.Ldsfld, _nativeFunctionsField);
            il.EmitFastI4Load(nativeFunctionIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            if(_options.loadingMode == DllLoadingMode.Lazy)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Call, _loadTargetFunctionMethod);
            }
            il.Emit(OpCodes.Ldfld, _nativeFunctionDelegateField);
            //Seems like no cast is required here

            for (int i = 0; i < parameterCount; i++)
            {
                il.EmitFastArgLoad(i);
            }

            il.Emit(OpCodes.Callvirt, delegateInvokeMethod);
            il.Emit(OpCodes.Ret);
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

        private static Type CreateDelegateTypeForNativeFunctionSignature(NativeFunctionSignature funcionSignature)
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
            var ufpAttrCtorArgValues = new object[] { funcionSignature.callingConvention };
            var ufpAttrNamedFields = new [] {
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.BestFitMapping), BindingFlags.Public | BindingFlags.Instance),
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.CharSet), BindingFlags.Public | BindingFlags.Instance),
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.SetLastError), BindingFlags.Public | BindingFlags.Instance),
                ufpAttrType.GetField(nameof(UnmanagedFunctionPointerAttribute.ThrowOnUnmappableChar), BindingFlags.Public | BindingFlags.Instance),
            };
            var ufpAttrFieldValues = new object[] { funcionSignature.bestFitMapping, funcionSignature.charSet, funcionSignature.setLastError, funcionSignature.throwOnUnmappableChar };
            var ufpAttrBuilder = new CustomAttributeBuilder(ufpAttrCtor, ufpAttrCtorArgValues, ufpAttrNamedFields, ufpAttrFieldValues);
            delBuilder.SetCustomAttribute(ufpAttrBuilder);


            var ctorBuilder = delBuilder.DefineConstructor(MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard, DELEGATE_CTOR_PARAMETERS);
            ctorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var invokeBuilder = delBuilder.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot,
                CallingConventions.Standard | CallingConventions.HasThis, funcionSignature.returnParameterType, funcionSignature.parameterTypes);
            invokeBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            _createdDelegateTypes++;
            return delBuilder.CreateType();
        }


        private static string GetDllPath(string dllName)
        {
            return _options.dllPathPattern
                .Replace(DLL_PATH_PATTERN_NAME_MACRO, dllName)
                .Replace(DLL_PATH_PATTERN_ASSETS_MACRO, Application.dataPath)
                .Replace(DLL_PATH_PATTERN_PROJECT_MACRO, Application.dataPath + "/../");
        }

        /// <summary>
        /// Loads DLL and function delegate of <paramref name="nativeFunction"/> if not yet loaded.
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

        private static IntPtr SysLoadDll(string filepath)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes.Windows_LoadLibrary(filepath);
#elif UNITY_STANDALONE_LINUX
            return PInvokes.Linux_dlopen(filepath, (int)_options.linuxDlopenFlags);
#endif
        }

        private static bool SysUnloadDll(IntPtr libHandle)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes.Windows_FreeLibrary(libHandle);
#elif UNITY_STANDALONE_LINUX
            return PInvokes.Linux_dlclose(libHandle) == 0;
#endif
        }

        private static IntPtr SysGetDllProcAddress(IntPtr libHandle, string symbol)
        {
#if UNITY_STANDALONE_WIN
            return PInvokes.Windows_GetProcAddress(libHandle, symbol);
#elif UNITY_STANDALONE_LINUX
            return PInvokes.Linux_dlsym(libHandle, symbol);
#endif
        }
    }

    [Serializable]
    public class DllManipulatorOptions
    {
        public string dllPathPattern;
        public DllLoadingMode loadingMode;
        public LinuxDlopenFlags linuxDlopenFlags;
        public bool mockAllNativeFunctions;
        public bool mockCallsInAllTypes;
    }

    public enum DllLoadingMode
    {
        Lazy,
        Preload
    }

    public enum LinuxDlopenFlags : int
    {
        Lazy = 0x00001,
        Now = 0x00002,
        Lazy_Global = 0x00100 | Lazy,
        Now_Global = 0x00100 | Now
    }
}