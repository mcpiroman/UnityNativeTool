# UnityNativeTool
Tool designed mainly to solve old problem with unloading native plugins without reopening Unity editor; with vary little changes to code base and low overhead. It enables to replace plugin files between game executions or even while playing.  
Doing so involves mocking original native method calls with these made to manually loaded libraries by P/Invokes. Although works in builded game, it's intended to use in edtior.

## Systems
- Windows
- Linux
- Mac (not tested)

## Requirements and dependencies
- [Harmony](https://github.com/pardeike/Harmony) (already included in release package)
- Api Compatibility Level >= .NET 4.x

## Limitations
- All parameter attributes other than `[MarshalAs]`, `[In]` and `[Out]` are not supported
- Fields `MarshalCookie`, `MarshalType`, `MarshalTypeRef` and `SafeArrayUserDefinedSubType` of `[MarshalAs]` attribute are not supported (due to Mono bug https://github.com/mono/mono/issues/12747)
- `[DllImport]` properties `ExactSpelling` and `PreserveSig` are not supported (as if anyone uses them)

## Preformance
Tested on old gaming laptop, Windows 10, 2 plugins with 10 functions each. Target function is simple addition of 2 floats and was called 1000000 times.

| Test case | Avarage time |
| --- |:---:|
| Without this tool | ~70ms |
| Lazy mode | ~135ms |
| Preload mode | ~105ms |
| With thread safety* | ~300ms |

*With uncontended locks.
## Instalation
1. Download and add unity package from [releases](https://github.com/MCpiroman/UnityNativeTool/releases).

2. Set _Api Compatibility Level_ to .NET 4.x or above.  
   Edit > Project Settings > Player > Other Settings > Api Compatibility Level

3. Set execution order of script `DllManipulator` to be the lowest of all scripts in game (at least of scripts that use native functions), e.g -10000.  
   Edit > Project Settings > Script Execution Order

4. One game object in the scene needs to have `DllManipulator` script on it. By default this script has `DontDestroayOnLoad(gameObject)` call, and deletes itself when dupliacate is found. You can simply disable that object if you don't want this tool to run.

## Usage
To native function to be mocked, both function declaration (extern method with `[DllImport]` attribute) and method which calls that function need to be suitably tagged with use of attributes below, alternatively `DllManipulator` options can be used. 

All plugins under controll of this tool will be unloaded once game stops. To unload/reloaded them manually use `DllManipulator` script in editor.

#### __Attributes__
  * `[MockNativeDeclarations]` - Tags all native functions within type with this attribute.
  * `[MockNativeDeclaration]` - Tags native function with this attribute.
  * `[MockNativeCalls]` - Tags calls to native functions made in all methods within type with this attribute.
  * `[DisableMocking]` - Disables mocking of native function with this attribute.
  
#### __Options__
These options are editable via `DllManipulator` script.
  * __DLL path pattern__ - Path at which mocked plugin files are located. Default is *Assets/Plugins/\__NameOfPlugin[.dll|.so|.dylib]*. Can be the same as path that Unity uses for plugins.
  * __DLL loading mode__ - Specifies how DLLs and functions will be loaded.
    + _Lazy_ - All DLLs and functions are loaded as they're first called. This allows them to be easily unloaded and loaded within game execution.
    + _Preloaded_ - Slight preformance benefit over _Lazy_ mode. All DLLs and functions are loaded at startup. Calls to unloaded DLLs lead to crash, so mid-execution it's safest to manipulate DLLs if game is paused.
  * __dlopen flags [Linux and Mac only]__ - Flags used in dlopen() P/Invoke on Linux and OSX systems. Has minor meaning unless library is large.
  * __Thread safe__ - When true, ensures synchronization required for native calls from any other than Unity main thread. Overhead might be few times higher, with uncontended locks. Available only in Preloaded mode.
  * __Mock all native functions__ - If true, all native functions in current assembly will be mocked.
  * __Mock native calls in all types__ - If true, calls of native funcions in all methods in current assembly will be mocked. This however can cause significant preformance issues at startup in big code base.
  
## Examples

```C#
using System.Runtime.InteropServices;
using UnityEngine;
using DllManipulator;

[MockNativeDeclarations]
class NativeFunctionsHolder
{
    [DllImport("MyNativePlugin")]
    public static extern int BigThought();

    [DllImport("MyNativePlugin", EntryPoint="complex_calc")]
    public static extern System.IntPtr ComplexCalc(float param1, Vector3 param2);
    
    [DisableMocking]
    [DllImport("MyNativePlugin")]
    public static extern void IWontBeMocked(bool ohReally);
}

class SomeMoreNatives
{
    [MockNativeDeclaration]
    [DllImport("MyNativePlugin")]
    public static extern void HereIAm(string where);
    
    [DllImport("MyNativePlugin")]
    public static extern bool WillIBeMocked();
}

[MockNativeCalls]
class FunctionsInvoker : MonoBehaviour
{
    void Start()
    {
        int answear = NativeFunctionsHolder.BigThought(); //This call will be mocked
        var calc = NativeFunctionsHolder.ComplexCalc(2, Vector3.up); //So will this
        NativeFunctionsHolder.IWontBeMocked(true); //As it says
    }
    
    void Update()
    {
        SomeMoreNatives.HereIAm("Wherever"); //This mocked too
        bool res = SomeMoreNatives.WillIBeMocked(); //Mocked only if "Mock all native functions" option is true
        MyOwnNative(); //As above
    }
    
    [DllImport("MyNativePlugin")]
    private static extern void MyOwnNative();
}

[MockNativeDeclarations]
[MockNativeCalls]
class AllInOne : MonoBehaviour
{
    void Start()
    {
        MyVeryOwnNative(); //Mocked
    }
  
    [DllImport("MyNativePlugin")]
    private static extern void MyVeryOwnNative();
}
```

## Planed features
- Native calls inlining
- Improved interthread synchronization
- Pausing on dll/function load error, allowing to fix depencency without restarting game
- Possibly break depencency on Harmony
- Better names