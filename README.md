Tool created mainly to solve old problem with reloading [native plugins](https://docs.unity3d.com/Manual/NativePlugins.html) without the need to reopen Unity Editor.

## Features / Overview

- Automatically unloads native plugins after stopping game and loads them when needed
- You can unload/reload them manually, even when game is playing
- No code change is required (use usual `[DllImport]`)
- [Low level interface](https://docs.unity3d.com/Manual/NativePluginInterface.html) callbacks `UnityPluginLoad` and `UnityPluginUnload` do fire
- Works on Windows, Linux and Mac
- Ability to log native calls to file in order to diagnose crashes caused by them

## Installation

1. Download and add unity package from [releases](https://github.com/MCpiroman/UnityNativeTool/releases), or clone this repo to your project

   - Clone it into the `<Project Root>/Packages` folder to use it as a local [embedded package](https://docs.unity3d.com/Manual/upm-embed.html) with [upm](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@1.8/manual/index.html).

   - Note that if you don't use the prebuilt package and you're willing to use [low level interface callbacks](https://docs.unity3d.com/Manual/NativePluginInterface.html), you'll need to compile the stub plugin yourself from `stubLluiPlugin.c`.

2. In project settings, set _Api Compatibility Level_ to .NET 4.x or above.  
   Edit > Project Settings > Player > Other Settings > Api Compatibility Level
   
3. Check _Allow 'unsafe' code_.  
   Edit > Project Settings > Player > Other Settings > Allow 'unsafe' code

4. One game object in the scene needs to have `DllManipulatorScript` on it. (This script calls `DontDestroayOnLoad(gameObject)` and deletes itself when duplicate is found, so you don't have to worry about switching scenes).

## Usage
- Your plugin files must be at path specified in options. By default, add __ (two underscores) at the beginning of your dll files in the Assets/Plugins folder (e.g. on Windows, plugin named `FastCalcs` should be at path `Assets\Plugins\__FastCalcs.dll`).
- By default, all native functions in main scripts assembly will be mocked (i.e. will be handled by this tool instead of Unity, which allows them to be unloaded). You can change this in options and use provided attributes to specify that yourself (these are in `UnityNativeTool`  namespace).
- Options are accessible via `DllManipulatorScript` editor or window.
- You can get callbacks in C# when the dll load state has changed with attributes like `[NativeDllLoadedTrigger]`. See `Attributes.cs`.
- Unload and load all DLLs via shortcut `Alt+D` and `Alt+Shfit+D` respectively. Editable in the Shortcut Manager for 2019.1+
- Although this tool presumably works in built game, it's intended to be used only during developement.
- If something is not working, first check out available options (and read their descriptions), then [report an issue](https://github.com/mcpiroman/UnityNativeTool/issues/new).

## Limitations
- Marshaling parameter attributes other than `[MarshalAs]`, `[In]` and `[Out]` are not supported.
- Properties `MarshalCookie`, `MarshalType`, `MarshalTypeRef` and `SafeArrayUserDefinedSubType` on `[MarshalAs]` attribute are not supported (due to [Mono bug](https://github.com/mono/mono/issues/12747)).
- Explicitly specifying `UnmanagedType.LPArray` in `[MarshalAs]` is not supported (due to [another Mono bug](https://github.com/mono/mono/issues/16570)). Note that this should be default for array types, so in trivial situations you wouldn't need to use it.
- Properties `ExactSpelling` and `PreserveSig` on `[DllImport]` attribute are not supported (as if anyone uses them).
- Native callbacks `UnityRenderingExtEvent` and `UnityRenderingExtQuery` are not fired.
- Calling native functions from static constuctors generally won't work. Although rules are more relaxed, you usually shouldn't even atempt to do that in the first place. Note that _[static constructors don't fire on their own](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-constructors#remarks)_.
- Threads that execute past `OnApplicationQuit` event are not-very-well handled (usually not something to worry about).

## Troubleshooting & advanced usage
- The path in the `DLL path pattern` option cannot be simply be set to `{assets}/Plugins/{name}.dll` as it would interfer with Unity's plugin loading. (That's why you need to bother with these undersocres.)
- Unity in version `2019.3.x` changed behaviour of building. If you want to use this tool in the builded game (although preferiably just for developement) you should store your plugins in architecture-specific subfolders and update the `DLL path pattern` option accordingly, e.g. `{assets}/Plugins/x86_64/__{name}.dll`.
- The `UnityNativeTool.DllManipulatorScript` script by default has execution order of -10000 to make it run first. If you have a script that has even lower execution order and that scripts calls a DLL, then you should make sure that `UnityNativeTool.DllManipulatorScript` runs before it, e.g. by further lowering its execution order.


## Performance

| Configuration | Relative call time |
| --- |:---:|
| Vanilla Unity | 100% |
| Preloaded mode | ~150% |
| Lazy mode | ~190% |
| With thread safety | ~430% |

## References
Some of the sources I based my code on:
- https://github.com/pardeike/Harmony
- https://stackoverflow.com/a/9507589/7249108
- http://runningdimensions.com/blog/?p=5
