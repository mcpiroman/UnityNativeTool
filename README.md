Tool created mainly to solve the old problem with reloading [native plugins](https://docs.unity3d.com/Manual/NativePlugins.html) without the need to reopen Unity Editor.

## Overview

- Automatically unloads native plugins after stopping the game and loads them when needed.
- You can unload/reload them manually, even when the game is running.
- No code change is required - use usual `[DllImport]`.
- [Low level interface](https://docs.unity3d.com/Manual/NativePluginInterface.html) callbacks `UnityPluginLoad` and `UnityPluginUnload` do fire - to enable them see [this section](#low-level-interface-callbacks-support).
- Works on Windows, Linux and Mac, but only on x86/x86_64 processors.
- Ability to log native calls to file in order to diagnose crashes caused by them.

## Installation

1. Either download and add unity package from [releases](https://github.com/MCpiroman/UnityNativeTool/releases), or clone this repo into the `assets` of your project.

   - Clone it into the `<Project Root>/Packages` folder to use it as a local [embedded package](https://docs.unity3d.com/Manual/upm-embed.html) with [upm](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@1.8/manual/index.html).

2. In project settings, set _Api Compatibility Level_ to .NET 4.x or above.
   Edit > Project Settings > Player > Other Settings > Api Compatibility Level
   
3. Check _Allow 'unsafe' code_.
   Edit > Project Settings > Player > Other Settings > Allow 'unsafe' code

4. One of the gameobjects in the scene needs to have `DllManipulatorScript` on it. (This script calls `DontDestroayOnLoad(gameObject)` and deletes itself when a duplicate is found in order to behave nicely when switching scenes).

## Usage
- Your plugin files must be at path specified in options. By default, just add `__` (two underscores) at the beginning of your dll files in the Assets/Plugins folder (e.g. on Windows, plugin named `FastCalcs` should be at path `Assets\Plugins\__FastCalcs.dll`).
- By default, all `extern` methods in the main scripts assembly will be mocked (i.e. handled by this tool instead of Unity, allowing them to be unloaded). You can change this in options and use provided attributes to specify that yourself (they are in `UnityNativeTool` namespace, file `Attributes.cs`).
- Options are accessible via `DllManipulatorScript` editor or window.
- You can also unload and load all DLLs via shortcut, `Alt+D` and `Alt+Shfit+D` respectively. Editable in the Shortcut Manager for 2019.1+
- You can get callbacks in your C# code when the load state of a DLL has changed with attributes like `[NativeDllLoadedTrigger]`. See `Attributes.cs`.
- Although this tool presumably works in the built game, it's intended to be used only during development.
- If something doesn't work, first check out available options (and read their descriptions), then [report an issue](https://github.com/mcpiroman/UnityNativeTool/issues/new).

## Low level interface callbacks support
For that, you'll need a `StubLluiPlugin` DLL. I only embed it into .unitypackage for x64 Windows platform, so for other cases you'll need to compile it manually.

This is, compile the file `./stubLluiPlugin.c` into the dynamic library (name it `StubLluiPlugin`, no underscores) and put into Unity like you would do with other plugins.

## Limitations
- Native callbacks `UnityRenderingExtEvent` and `UnityRenderingExtQuery` do not fire.
- Only some basic attributes on parameters of `extern` methods (such as `[MarshalAs]` or `[In]`) are supported.
- Properties `MarshalCookie`, `MarshalType`, `MarshalTypeRef` and `SafeArrayUserDefinedSubType` on `[MarshalAs]` attribute are not supported (due to [Mono bug](https://github.com/mono/mono/issues/12747)).
- Explicitly specifying `UnmanagedType.LPArray` in `[MarshalAs]` is not supported (due to [another Mono bug](https://github.com/mono/mono/issues/16570)). Note that this should be the default for array types, so in trivial situations you don't need to use it anyway.
- Properties `ExactSpelling` and `PreserveSig` of `[DllImport]` attribute are not supported.
- Calling native functions from static constructors generally won't work. Although the rules are more relaxed, you usually shouldn't even attempt to do that in the first place. Note that in C# _[static constructors don't fire on their own](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-constructors#remarks)_.
- Additional threads that execute past `OnApplicationQuit` event are not-very-well handled (usually not something to worry about).

## Troubleshooting & advanced usage
- The path in the `DLL path pattern` option cannot be simply set to `{assets}/Plugins/{name}.dll` as it would interfere with Unity's plugin loading - hence the underscores.
- In version `2019.3.x` Unity changed behaviour of building. If you want to use this tool in the built game (although preferably just for development) you should store your plugins in architecture-specific subfolders and update the `DLL path pattern` option accordingly, e.g. `{assets}/Plugins/x86_64/__{name}.dll`.
- The `UnityNativeTool.DllManipulatorScript` script by default has an execution order of -10000 to make it run first. If you have a script that has even lower execution order and that scripts calls a DLL, then you should make sure that `UnityNativeTool.DllManipulatorScript` runs before it, e.g. by further lowering its execution order.


## Performance

| Configuration | Relative call time |
| --- |:---:|
| Vanilla Unity | 100% |
| Preloaded mode | ~150% |
| Lazy mode | ~190% |
| With thread safety | ~430% |

## References
- https://github.com/pardeike/Harmony
- https://stackoverflow.com/a/9507589/7249108
- http://runningdimensions.com/blog/?p=5
