using System;

namespace DllManipulator
{
    /// <summary>
    /// Calls of all native functions declared inside types with this attributes may be mocked. This attribute is redundant if <see cref="DllManipulatorOptions.mockAllNativeFunctions"/> is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MockNativeDeclarationsAttribute : Attribute
    {

    }

    /// <summary>
    /// Calls of native functions with this attribute may be mocked. This attribute is redundant if <see cref="DllManipulatorOptions.mockAllNativeFunctions"/> is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class MockNativeDeclarationAttribute : Attribute
    {

    }

    /// <summary>
    /// Calls of native functions with this attribute won't be mocked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class DisableMockingAttribute : Attribute
    {

    }
}
