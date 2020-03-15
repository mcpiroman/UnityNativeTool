namespace UnityNativeTool
{
    /// <summary>
    /// A place to implement you custom callbacks.
    /// </summary>
    public static class DllCallbacks
    {
        /// <summary>
        /// This is called whenever a dll has been loaded.
        /// </summary>
        /// <param name="dllName">The name without preceding underscores or file extension.</param>
        public static void OnDllLoaded(string dllName)
        {
            // Debug.Log("[dll] " + dllName + " dll loaded.");
        }
        
        /// <summary>
        /// This is called whenever a dll is about to be unloaded.
        /// </summary>
        /// <param name="dllName">The name without preceding underscores or file extension.</param>
        public static void OnBeforeDllUnload(string dllName)
        {
            // Debug.Log("[dll] " + dllName + " dll unloading...");
        }
        
        /// <summary>
        /// This is called whenever a dll has been unloaded.
        /// </summary>
        /// <param name="dllName">The name without preceding underscores or file extension.</param>
        public static void OnAfterDllUnload(string dllName)
        {
            // Debug.Log("[dll] " + dllName + " dll unloaded.");
        }
    }
}