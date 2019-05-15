using System;

namespace UnityNativeTool.Internal
{
    internal static class DefaultIgnoredDllPaths
    {
        public static readonly string[] VALUE =
        {
#if UNITY_STANDALONE_WIN
             Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\**\\*.dll",
             Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\**\\*.dll",
             Environment.GetFolderPath(Environment.SpecialFolder.SystemX86) + "\\**\\*.dll",
#elif UNITY_STANDALONE_LINUX

#elif UNITY_STANDALONE_OSX
#endif
        };
    }
}