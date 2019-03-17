# UnityNativeTool
Tool created mainly to solve old problem with unloading native plugins without reopening Unity Editor.

## Features
- Automaticly unloads native plugins after stopping game and loads them when needed
- You can unload/reload them manually in playing or paused state
- Works on Windows, Linux and Mac
- No code change is required (use usual `[DllImport]`)
- Ability to log native calls to file in order to diagnose crashes caused by them

## Requirements
- Api Compatibility Level &ge; .NET 4.x
- [Unsafe](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/unsafe-code-pointers/index) code

## Limitations
- Marshaling parameter attributes other than `[MarshalAs]`, `[In]` and `[Out]` are not supported
- Fields `MarshalCookie`, `MarshalType`, `MarshalTypeRef` and `SafeArrayUserDefinedSubType` of `[MarshalAs]` attribute are not supported (due to Mono bug https://github.com/mono/mono/issues/12747)
- `[DllImport]` properties `ExactSpelling` and `PreserveSig` are not supported (as if anyone uses them)
- Threads that execute past `OnApplicationQuit` event are not-very-well handled (usualy not something to worry about)

## Preformance
Tested on old gaming laptop, Windows 10, 2 plugins with 10 functions each. Target function is simple addition of 2 floats and was called 1000000 times.

| Test case | Avarage time |
| --- |:---:|
| Without this tool | ~70ms |
| Lazy mode | ~135ms |
| Preload mode | ~105ms |
| With thread safety | ~300ms |

## Instalation
1. Download and add unity package from [releases](https://github.com/MCpiroman/UnityNativeTool/releases).

2. In project settings, set _Api Compatibility Level_ to .NET 4.x or above.  
   Edit > Project Settings > Player > Other Settings > Api Compatibility Level
   
3. Set _Allow 'unsafe' code_ to true.  
   Edit > Project Settings > Player > Other Settings > Allow 'unsafe' code

4. Set execution order of script `DllManipulator` to be the lowest of all scripts in game (at least of scripts that use native functions), e.g -10000.  
   Edit > Project Settings > Script Execution Order

5. One game object in the scene needs to have `DllManipulator` script on it. By default this script has `DontDestroayOnLoad(gameObject)` call, and deletes itself when dupliacate is found. 

## Usage
- Your plugin files must be at path specified in options. By default you just add __ at the beginning of file name.
- By default, all native functions in scripts assembly will be mocked (i.e. will be handled by this tool instead of Unity or Mono). You can change that in options and use attributes to select which functions you want to be mocked.
- You can simply disactivate `DllManipulator` object if you don't want this tool to run.
- Although presumably runs in builded game, it's intended to be used in editor.
  
#### __Options__
Options are accessed via `DllManipulator` script editor or window.
  * __DLL path pattern__ - Path at which mocked plugin files are located. Default is *Assets/Plugins/\__NameOfPlugin[.dll|.so|.dylib]*. Can be the same as path that Unity uses for plugins.
  * __DLL loading mode__ - Specifies how DLLs and functions will be loaded.
    + _Lazy_ - All DLLs and functions are loaded each time they are called, if not loaded yet. This allows them to be easily unloaded and loaded within game execution.
    + _Preloaded_ - Slight preformance benefit over _Lazy_ mode. All declared DLLs and functions are loaded at startup and are not automaticly reloaded.
  * __dlopen flags [Linux and Mac only]__ - Flags used in dlopen() P/Invoke on Linux and OSX systems. Has minor meaning unless library is large.
  * __Crash logs__ - Logs each native call to file. In case of crash or hang caused by native function, you can than see what function was that, along with arguments and, optionally, stack trace. In multi-threaded scenario there will be one file for each thread and you'll have to guess the right one (call index will be a hint). Note that existence of log files doesn't mean the crash was caused by any tracked native function. Overhead is HIGH and depends on OS and disk (on poor PC there might be just few native calls per update to disturb 60 fps.)
  * __Thread safe__ - When true, ensures synchronization required for native calls from any other than Unity main thread. Overhead might be few times higher, with uncontended locks. Available only in Preloaded mode.
  * __Mock all native functions__ - If true, all native functions in current assembly will be mocked. If false, use attributes.

#### __Attributes__
  * `[MockNativeDeclarations]` - Mocks all native functions within type with this attribute.
  * `[MockNativeDeclaration]` - Mocks native function with this attribute.
  * `[DisableMocking]` - Disables mocking of native function with this attribute. Can be used on whole classes.

## Planned/possible features
- Debugging native functions
- Improved thread safety and interthread synchronization
- Pausing on dll/function load error, allowing to fix depencency without restarting game
- Native calls inlining
- Better names
