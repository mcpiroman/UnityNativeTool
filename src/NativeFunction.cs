using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DllManipulator.Internal
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

    internal class NativeFunctionSignature
    {
        public readonly Type returnParameterType;
        public readonly Type[] returnParameterRequiredModifiers;
        public readonly Type[] retunrParameterOptionalModifiers;
        public readonly Type[] parameterTypes;
        public readonly Type[][] parameterRequiredModifiers;
        public readonly Type[][] parameterOptionalModifiers;
        public readonly System.Runtime.InteropServices.CallingConvention callingConvention;

        public NativeFunctionSignature(MethodInfo methodInfo, System.Runtime.InteropServices.CallingConvention callingConvention)
        {
            var returnParameter = methodInfo.ReturnParameter;
            returnParameterType = returnParameter.ParameterType;
            returnParameterRequiredModifiers = returnParameter.GetRequiredCustomModifiers();
            retunrParameterOptionalModifiers = returnParameter.GetOptionalCustomModifiers();
            var parameters = methodInfo.GetParameters();
            parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            parameterRequiredModifiers = new Type[parameters.Length][];
            parameterOptionalModifiers = new Type[parameters.Length][];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterRequiredModifiers[i] = parameters[i].GetRequiredCustomModifiers();
                parameterOptionalModifiers[i] = parameters[i].GetOptionalCustomModifiers();
            }

            this.callingConvention = callingConvention;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is NativeFunctionSignature other)
            {
                return Equals(other);
            }

            return false;
        }

        public bool Equals(NativeFunctionSignature other)
        {
            //Ordered by probability in being different
            if (returnParameterType != other.returnParameterType)
            {
                return false;
            }

            if (!parameterTypes.SequenceEqual(other.parameterTypes))
            {
                return false;
            }

            if (parameterRequiredModifiers.Length != other.parameterRequiredModifiers.Length)
            {
                return false;
            }

            for (int i = 0; i < parameterRequiredModifiers.Length; i++)
            {
                if (!parameterRequiredModifiers[i].SequenceEqual(other.parameterRequiredModifiers[i]))
                {
                    return false;
                }
            }

            if (!returnParameterRequiredModifiers.SequenceEqual(other.returnParameterRequiredModifiers))
            {
                return false;
            }

            if (callingConvention != other.callingConvention)
            {
                return false;
            }

            if (parameterOptionalModifiers.Length != other.parameterOptionalModifiers.Length)
            {
                return false;
            }

            for (int i = 0; i < parameterOptionalModifiers.Length; i++)
            {
                if (!parameterOptionalModifiers[i].SequenceEqual(other.parameterOptionalModifiers[i]))
                {
                    return false;
                }
            }

            if (!retunrParameterOptionalModifiers.SequenceEqual(other.retunrParameterOptionalModifiers))
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = 1660825957;
            hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(returnParameterType);
            hashCode = hashCode * -1521134295 + EqualityComparer<Type[]>.Default.GetHashCode(returnParameterRequiredModifiers);
            hashCode = hashCode * -1521134295 + EqualityComparer<Type[]>.Default.GetHashCode(retunrParameterOptionalModifiers);
            hashCode = hashCode * -1521134295 + EqualityComparer<Type[]>.Default.GetHashCode(parameterTypes);
            hashCode = hashCode * -1521134295 + EqualityComparer<Type[][]>.Default.GetHashCode(parameterRequiredModifiers);
            hashCode = hashCode * -1521134295 + EqualityComparer<Type[][]>.Default.GetHashCode(parameterOptionalModifiers);
            hashCode = hashCode * -1521134295 + callingConvention.GetHashCode();
            return hashCode;
        }
    }
}
