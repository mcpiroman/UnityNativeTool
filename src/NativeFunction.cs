using System;

namespace UnityNativeTool.Internal
{
    internal struct NativeFunctionIdentity
    {
        public string symbol;
        public string containingDllName;

        public NativeFunctionIdentity(string symbol, string containingDllName)
        {
            this.symbol = symbol;
            this.containingDllName = containingDllName;
        }

        public override bool Equals(object obj)
        {
            if (obj is NativeFunctionIdentity other)
            {
                return symbol == other.symbol && containingDllName == other.containingDllName;
            }

            return false;
        }

        public override int GetHashCode()
        {
            int h1 = symbol.GetHashCode();
            int h2 = containingDllName.GetHashCode();
            uint num = (uint)((h1 << 5) | (int)((uint)h1 >> 27));
            return ((int)num + h1) ^ h2;
        }
    }

    internal class NativeFunction
    {
        public readonly NativeFunctionIdentity identity;
        public NativeDll containingDll;
        public Delegate @delegate = null;
        public Type delegateType = null;
        public int index = -1;

        public NativeFunction(NativeFunctionIdentity identity, NativeDll containingDll)
        {
            this.identity = identity;
            this.containingDll = containingDll;
        }
    }
}
