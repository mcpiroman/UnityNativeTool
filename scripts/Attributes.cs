using System;

namespace UnityNativeTool
{
    /// <summary>
    /// Member native functions in types with this attributes will be mocked. This attribute is redundant if "Mock all native functions" option is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class MockNativeDeclarationsAttribute : Attribute
    {

    }

    /// <summary>
    /// Native functions with this attribute will be mocked. This attribute is redundant if "Mock all native functions" option is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class MockNativeDeclarationAttribute : Attribute
    {

    }

    /// <summary>
    /// Applied to native function, prevents it from being mocked.
    /// Applied to class, prevents all member native functions from being mocked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class DisableMockingAttribute : Attribute
    {

    }

    /// <summary>
    /// Methods with this attribute are called directly after a native DLL has been loaded.
    /// Such method must be <see langword="static"/> and either have no parameters or one parameter of type <see cref="NativeDll"/>
    /// which indicates the state of the dll being loaded. Please treat this parameter as readonly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class NativeDllLoadedTriggerAttribute : Attribute
    {

    }

    /// <summary>
    /// Methods with this attribute are called directly before a native DLL is going to be unloaded.
    /// Such method must be <see langword="static"/> and either have no parameters or one parameter of type <see cref="NativeDll"/>
    /// which indicates the state of the dll being unloaded. Please treat this parameter as readonly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class NativeDllBeforeUnloadTriggerAttribute : Attribute
    {

    }

    /// <summary>
    /// Methods with this attribute are called directly after a native DLL has been unloaded.
    /// Such method must be <see langword="static"/> and either have no parameters or one parameter of type <see cref="NativeDll"/>
    /// which indicates the state of the dll being unloaded. Please treat this parameter as readonly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class NativeDllAfterUnloadTriggerAttribute : Attribute
    {

    }
}
