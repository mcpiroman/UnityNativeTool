using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

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
        public readonly Type[] parameterTypes;
        public readonly CallingConvention callingConvention;
        public readonly bool bestFitMapping;
        public readonly CharSet charSet;
        public readonly bool setLastError;
        public readonly bool throwOnUnmappableChar;

        public NativeFunctionSignature(MethodInfo methodInfo, CallingConvention callingConvention, bool bestFitMapping, CharSet charSet, bool setLastError, bool throwOnUnmappableChar)
        {
            this.returnParameterType = methodInfo.ReturnType;
            this.parameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            this.callingConvention = callingConvention;
            this.bestFitMapping = bestFitMapping;
            this.charSet = charSet;
            this.setLastError = setLastError;
            this.throwOnUnmappableChar = throwOnUnmappableChar;
        }

        public override bool Equals(object obj)
        {
            return obj is NativeFunctionSignature other &&
                   EqualityComparer<Type>.Default.Equals(returnParameterType, other.returnParameterType) &&
                   EqualityComparer<Type[]>.Default.Equals(parameterTypes, other.parameterTypes) &&
                   callingConvention == other.callingConvention &&
                   bestFitMapping == other.bestFitMapping &&
                   charSet == other.charSet &&
                   setLastError == other.setLastError &&
                   throwOnUnmappableChar == other.throwOnUnmappableChar;
        }

        public override int GetHashCode()
        {
            var hashCode = 763644728;
            hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(returnParameterType);
            hashCode = hashCode * -1521134295 + EqualityComparer<Type[]>.Default.GetHashCode(parameterTypes);
            hashCode = hashCode * -1521134295 + callingConvention.GetHashCode();
            hashCode = hashCode * -1521134295 + bestFitMapping.GetHashCode();
            hashCode = hashCode * -1521134295 + charSet.GetHashCode();
            hashCode = hashCode * -1521134295 + setLastError.GetHashCode();
            hashCode = hashCode * -1521134295 + throwOnUnmappableChar.GetHashCode();
            return hashCode;
        }
    }
}
