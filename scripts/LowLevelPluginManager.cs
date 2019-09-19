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
            catch(DllNotFoundException ex)
            {
                Debug.Log(ex);
            }

            Debug.Log("Initialize()");
        }

        public static void OnDllLoaded(NativeDll dll)
        {
            Debug.Log("OnDllLoaded() " + _unityInterfacePtr);

            if (_unityInterfacePtr == IntPtr.Zero)
                return;

            var unityPluginLoadFunc = new NativeFunction("UnityPluginLoad", dll) {
                delegateType = typeof(UnityPluginLoadDel)
            };

            DllManipulator.LoadTargetFunction(unityPluginLoadFunc, true);
            if (unityPluginLoadFunc.@delegate != null)
            {
                ((UnityPluginLoadDel)unityPluginLoadFunc.@delegate)(_unityInterfacePtr);
                Debug.Log("Called UnityPluginLoad");
            }
        }

        public static void OnBeforeDllUnload(NativeDll dll)
        {
            var unityPluginUnloadFunc = new NativeFunction("UnityPluginUnload", dll) {
                delegateType = typeof(UnityPluginUnloadDel)
            };

            DllManipulator.LoadTargetFunction(unityPluginUnloadFunc, true);
            if (unityPluginUnloadFunc.@delegate != null)
            { 
                Debug.Log("Called UnityPluginUnload");
            }
        }

        delegate void UnityPluginLoadDel(IntPtr unityInterfaces);
        delegate void UnityPluginUnloadDel();
        
        [DllImport("StubLluiPlugin")]
        private static extern IntPtr GetUnityInterfacesPtr();
    }
}
