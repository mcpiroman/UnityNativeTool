using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DllManipulator.Internal
{
    internal class NativeFunctionSignature
    {
        public readonly NativeFunctionParameterSignature returnParameter;
        public readonly NativeFunctionParameterSignature[] parameters;
        public readonly CallingConvention callingConvention;
        public readonly bool bestFitMapping;
        public readonly CharSet charSet;
        public readonly bool setLastError;
        public readonly bool throwOnUnmappableChar;

        public NativeFunctionSignature(MethodInfo methodInfo, CallingConvention callingConvention, bool bestFitMapping, CharSet charSet, bool setLastError, bool throwOnUnmappableChar)
        {
            this.returnParameter = new NativeFunctionParameterSignature(methodInfo.ReturnParameter);
            this.parameters = methodInfo.GetParameters().Select(p => new NativeFunctionParameterSignature(p)).ToArray();
            this.callingConvention = callingConvention;
            this.bestFitMapping = bestFitMapping;
            this.charSet = charSet;
            this.setLastError = setLastError;
            this.throwOnUnmappableChar = throwOnUnmappableChar;
        }

        public override bool Equals(object obj)
        {
            return obj is NativeFunctionSignature other &&
                   returnParameter.Equals(other.returnParameter) &&
                   parameters.SequenceEqual(other.parameters) &&
                   callingConvention == other.callingConvention &&
                   bestFitMapping == other.bestFitMapping &&
                   charSet == other.charSet &&
                   setLastError == other.setLastError &&
                   throwOnUnmappableChar == other.throwOnUnmappableChar;
        }

        public override int GetHashCode()
        {
            var hashCode = -1225548256;
            hashCode = hashCode * -1521134295 + returnParameter.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<NativeFunctionParameterSignature[]>.Default.GetHashCode(parameters);
            hashCode = hashCode * -1521134295 + callingConvention.GetHashCode();
            hashCode = hashCode * -1521134295 + bestFitMapping.GetHashCode();
            hashCode = hashCode * -1521134295 + charSet.GetHashCode();
            hashCode = hashCode * -1521134295 + setLastError.GetHashCode();
            hashCode = hashCode * -1521134295 + throwOnUnmappableChar.GetHashCode();
            return hashCode;
        }
    }

    internal class NativeFunctionParameterSignature
    {
        public readonly Type type;
        public readonly ParameterAttributes parameterAttributes;
        public readonly Attribute[] customAttributes;

        public NativeFunctionParameterSignature(ParameterInfo parameterInfo)
        {
            this.type = parameterInfo.ParameterType;
            this.parameterAttributes = parameterInfo.Attributes;
            this.customAttributes = parameterInfo.GetCustomAttributes()
                .Where(a => DllManipulator.SUPPORTED_PARAMATER_ATTRIBUTES.Contains(a.GetType()))
                .ToArray();
        }

        public NativeFunctionParameterSignature(Type type, ParameterAttributes parameterAttributes, Attribute[] customAttributes)
        {
            this.type = type;
            this.parameterAttributes = parameterAttributes;
            this.customAttributes = customAttributes;
        }

        public override bool Equals(object obj)
        {
            var other = obj as NativeFunctionParameterSignature;
            return other != null &&
                   type == other.type &&
                   parameterAttributes == other.parameterAttributes &&
                   customAttributes.Except(other.customAttributes).Any(); //Check if arrays have the same elements
        }

        public override int GetHashCode()
        {
            var hashCode = 1477582057;
            hashCode = hashCode * -1521134295 + type.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<Attribute[]>.Default.GetHashCode(customAttributes);
            hashCode = hashCode * -1521134295 + customAttributes.GetHashCode();
            return hashCode;
        }
    }
}
