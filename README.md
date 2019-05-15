Tool created mainly to solve old problem with unloading [native plugins](https://docs.unity3d.com/Manual/NativePlugins.html) without the need to reopen Unity Editor.

## Features
- Automaticly unloads native plugins after stopping game and loads them when needed
- You can unload/reload them manually in playing or paused state
- No code change is required (use usual `[DllImport]`)
- Works on Windows, Linux and Mac
- Ability to log native calls to file in order to diagnose crashes caused by them

## Instalation
1. Download and add unity package from [releases](https://github.com/MCpiroman/UnityNativeTool/releases).

2. In project settings, set _Api Compatibility Level_ to .NET 4.x or above.  
   Edit > Project Settings > Player > Other Settings > Api Compatibility Level
   
3. Check _Allow 'unsafe' code_.  
   Edit > Project Settings > Player > Other Settings > Allow 'unsafe' code

4. Set execution order of script `UnityNativeTool.DllManipulatorScript` to be the lowest of all scripts in game (at least of scripts that use native functions), e.g -10000.  
   Edit > Project Settings > Script Execution Order

5. One game object in the scene needs to have `DllManipulatorScript` on it. This script has `DontDestroayOnLoad(gameObject)` call, and deletes itself when dupliacate is found.

## Usage
- Your plugin files must be at path specified in options. By default you add __ (two underscores) at the beginning of your dll file in Assets/Plugins folder.
- By default, all native functions in main scripts assembly will be mocked (i.e. will be handled by this tool instead of Unity). You can change that in options and use provided attributes to specify it by yourself (these are in `UnityNativeTool`  namespace).
- If something is not working, first check out available options (and read their description), then [report an issue](https://github.com/mcpiroman/UnityNativeTool/issues/new).
- Options are accessible via `DllManipulatorScript` editor or window.
- Although presumably runs in builded game, it's intended to be used in editor.

## Limitations
- Marshaling parameter attributes other than `[MarshalAs]`, `[In]` and `[Out]` are not supported
- `[MarshalAs]` attribute fields: `MarshalCookie`, `MarshalType`, `MarshalTypeRef` and `SafeArrayUserDefinedSubType` are not supported (due to Mono bug https://github.com/mono/mono/issues/12747)
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

## Planned/possible features
- Seamless native code debugging
- Improved thread safety and interthread synchronization
- Pausing on dll/function load error, allowing to fix depencency without restarting game
- Diffrent mocking methods (IL/metadata/assembly manipulation)
- Unit tests

## References
Some of the sources I based my code on (as you often say, this wouldn't be possible without):
- https://github.com/pardeike/Harmony
- https://stackoverflow.com/a/9507589/7249108
- http://runningdimensions.com/blog/?p=5
