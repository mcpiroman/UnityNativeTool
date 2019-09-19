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
}
