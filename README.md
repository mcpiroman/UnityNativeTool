Tool created mainly to solve old problem with reloading [native plugins](https://docs.unity3d.com/Manual/NativePlugins.html) without the need to reopen Unity Editor.

## Features / overview
- Automaticly unloads native plugins after stopping game and loads them when needed
- You can unload/reload them manually, even when game is playing
- No code change is required (use usual `[DllImport]`)
- [Low level interface](https://docs.unity3d.com/Manual/NativePluginInterface.html) callbacks `UnityPluginLoad` and `UnityPluginUnload` do fire
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

5. One game object in the scene needs to have `DllManipulatorScript` on it. (This script calls `DontDestroayOnLoad(gameObject)` and deletes itself when dupliacate is found, so you don't have to worry about switching scenes).

## Usage
- Your plugin files must be at path specified in options. By default, add __ (two underscores) at the beginning of your dll files in the Assets/Plugins folder (e.g. on Windows, plugin named `FastCalcs` should be at path `Assets\Plugins\__FastCalcs.dll`).
- By default, all native functions in main scripts assembly will be mocked (i.e. will be handled by this tool instead of Unity, which allows them to be unloaded). You can change this in options and use provided attributes to specify that by yourself (these are in `UnityNativeTool`  namespace).
- If something is not working, first check out available options (and read their descriptions), then [report an issue](https://github.com/mcpiroman/UnityNativeTool/issues/new).
- Options are accessible via `DllManipulatorScript` editor or window.
- Although this tool presumably works in builded game, it's intended to be used in editor.

## Limitations
- [Low level callbacks](https://docs.unity3d.com/Manual/NativePluginInterface.html) such as `UnityPluginLoad` are not supported (right now).
- Marshaling parameter attributes other than `[MarshalAs]`, `[In]` and `[Out]` are not supported.
- Properties `MarshalCookie`, `MarshalType`, `MarshalTypeRef` and `SafeArrayUserDefinedSubType` on `[MarshalAs]` attribute are not supported (due to [Mono bug](https://github.com/mono/mono/issues/12747)).
- Explicitly specifying `UnmanagedType.LPArray` in `[MarshalAs]` is not supported (due to [another Mono bug](https://github.com/mono/mono/issues/16570)). Note that this should be default for array types, so in trivial situations you wouldn't need to use it.
- Properties `ExactSpelling` and `PreserveSig` on `[DllImport]` attribute are not supported (as if anyone uses them).
- `UnityRenderingExtEvent` and `UnityRenderingExtQuery` callbacks don't fire.
- Threads that execute past `OnApplicationQuit` event are not-very-well handled (usualy not something to worry about).

## Preformance

| Configuration | Relative call time |
| --- |:---:|
| Vanilla Unity | 100% |
| Preloaded mode | ~150% |
| Lazy mode | ~190% |
| With thread safety | ~430% |

## Planned / possible features
- Seamless managed/native code debugging
- Improved thread safety and interthread synchronization
- Diffrent mocking methods (IL/metadata/assembly manipulation)
- Unit tests (uhh, hard)

## References
Some of the sources I based my code on (as you often say, this wouldn't be possible without):
- https://github.com/pardeike/Harmony
- https://stackoverflow.com/a/9507589/7249108
- http://runningdimensions.com/blog/?p=5
