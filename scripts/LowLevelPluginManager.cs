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
    [DisableMocking]
    class LowLevelPluginManager
    {
        private static IntPtr _unityInterfacePtr = IntPtr.Zero;
        public static void Initialize()
        {
            try
            {
                _unityInterfacePtr = GetUnityInterfacesPtr();
                if (_unityInterfacePtr == IntPtr.Zero)
                    throw new Exception($"{nameof(GetUnityInterfacesPtr)} returned null");
            }
            catch(DllNotFoundException)
            {
                Debug.LogWarning("StubLluiPlugin not found. UnityPluginLoad and UnityPluginUnload callbacks won't fire. You may comment out this warning if you don't care about these callbacks.");
            }
        }

        public static void OnDllLoaded(NativeDll dll)
        {
            if (_unityInterfacePtr == IntPtr.Zero)
                return;

            var unityPluginLoadFunc = new NativeFunction("UnityPluginLoad", dll) {
                delegateType = typeof(UnityPluginLoadDel)
            };

            DllManipulator.LoadTargetFunction(unityPluginLoadFunc, true);
            if (unityPluginLoadFunc.@delegate != null)
                ((UnityPluginLoadDel)unityPluginLoadFunc.@delegate)(_unityInterfacePtr);
        }

        public static void OnBeforeDllUnload(NativeDll dll)
        {
            var unityPluginUnloadFunc = new NativeFunction("UnityPluginUnload", dll) {
                delegateType = typeof(UnityPluginUnloadDel)
            };

            DllManipulator.LoadTargetFunction(unityPluginUnloadFunc, true);
            if (unityPluginUnloadFunc.@delegate != null)
                ((UnityPluginUnloadDel)unityPluginUnloadFunc.@delegate)();
        }

        delegate void UnityPluginLoadDel(IntPtr unityInterfaces);
        delegate void UnityPluginUnloadDel();
        
        [DllImport("StubLluiPlugin")]
        private static extern IntPtr GetUnityInterfacesPtr();
    }
}
