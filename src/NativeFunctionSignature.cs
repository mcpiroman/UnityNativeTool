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
            var other = obj as NativeFunctionSignature;
            if (other == null)
            {
                return false;
            }

            if(!returnParameter.Equals(other.returnParameter))
            {
                return false;
            }

            if (!parameters.SequenceEqual(other.parameters))
            {
                return false;
            }

            if (callingConvention != other.callingConvention)
            {
                return false;
            }

            if (bestFitMapping != other.bestFitMapping)
            {
                return false;
            }

            if (charSet != other.charSet)
            {
                return false;
            }

            if (setLastError != other.setLastError)
            {
                return false;
            }

            if (throwOnUnmappableChar != other.throwOnUnmappableChar)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = 316391695;
            hashCode = hashCode * -1521134295 + returnParameter.GetHashCode();
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
            var attrs = parameterInfo.GetCustomAttributes(false).OfType<Attribute>(); //XXX: This is required way of obtaining attributes, since both CustomAttributeExtensions.GetCustomAttributes() and Attribute.GetCustomAttributes() return at most 1 attribute (mono bug?)
            this.customAttributes = attrs
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
            if(other == null)
            {
                return false;
            }

            if (type != other.type)
            {
                return false;
            }

            if (parameterAttributes != other.parameterAttributes)
            {
                return false;
            }
            
            if (customAttributes.Except(other.customAttributes).Any()) //Check if arrays have the same elements
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = 424392846;
            hashCode = hashCode * -1521134295 + type.GetHashCode();
            hashCode = hashCode * -1521134295 + parameterAttributes.GetHashCode();
            return hashCode;
        }
    }
}
