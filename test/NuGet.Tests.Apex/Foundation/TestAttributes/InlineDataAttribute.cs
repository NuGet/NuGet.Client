using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class InlineDataAttribute : DataAttribute
    {
        public static void ShimObjectArray(ref object[] data)
        {
            if (data != null && data.Length > 0)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == null)
                    {
                        continue;
                    }

                    Type t = data[i].GetType();
                    bool isEnum = t.IsEnum;

                    if (isEnum)
                    {
                        data[i] = new EnumShim((Enum)data[i]);
                    }
                    else if (data[i] is Type)
                    {
                        data[i] = new TypeShim((Type)data[i]);
                    }
                }
            }
        }

        private static bool IsFromLocalAssembly(Type type)
        {
            var assemblyName = type.Assembly.GetName().Name;

            try
            {
                Assembly.Load(assemblyName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private readonly object[] data;

        /// <summary>
        /// Initializes a new instance of the <see cref="InlineDataAttribute"/> class.
        /// </summary>
        /// <param name="data">The data values to pass to the theory.</param>
        public InlineDataAttribute(params object[] data)
        {
            InlineDataAttribute.ShimObjectArray(ref data);

            this.data = data;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            return new[] { data };
        }

        /// <summary>
        /// This class exists so that we can see the testcases that use Type from "GACed" assemblies that XUnit will not bother trying to do anything with
        /// in the VS test runner
        /// </summary>
        public class TypeShim : IXunitSerializable, IConvertible
        {
            private Type type;
            private int hash;
            private TypeCode typeCode;

            [EditorBrowsable(EditorBrowsableState.Never)]
            [Obsolete("Called by the de-serializer", true)]
            public TypeShim()
            {
            }

            public TypeShim(Type type)
            {
                this.type = type;
                this.hash = type.Name.GetHashCode();
                this.typeCode = Type.GetTypeCode(type);
            }

            public object GetUnderlyingType()
            {
                return this.type;
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                this.type = Type.GetType(info.GetValue<string>("TypeAssemblyQualifiedName"));
                this.hash = info.GetValue<int>("Hash");
                this.typeCode = (TypeCode)Enum.Parse(typeof(TypeCode), info.GetValue<string>("TypeCode"));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue("TypeAssemblyQualifiedName", type.AssemblyQualifiedName);
                info.AddValue("Hash", this.hash);
                info.AddValue("TypeCode", this.typeCode.ToString());
            }

            public override string ToString()
            {
                return type.Name;
            }

            public override int GetHashCode()
            {
                return this.hash;
            }

            public TypeCode GetTypeCode()
            {
                return this.typeCode;
            }

            public string ToString(IFormatProvider provider)
            {
                return this.ToString();
            }

            public object ToType(Type conversionType, IFormatProvider provider)
            {
                return this.type;
            }

            #region NotImplemented
            public bool ToBoolean(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public char ToChar(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public sbyte ToSByte(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public byte ToByte(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public short ToInt16(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public ushort ToUInt16(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public int ToInt32(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public uint ToUInt32(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public long ToInt64(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public ulong ToUInt64(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public float ToSingle(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public double ToDouble(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public decimal ToDecimal(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public DateTime ToDateTime(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }
            #endregion NotImplemented
        }

        /// <summary>
        /// This class exists so that we can see the testcases that use Enums from "GACed" assemblies that XUnit will not bother trying to do anything with
        /// in the VS test runner
        /// </summary>
        public class EnumShim : IXunitSerializable, IConvertible
        {
            [EditorBrowsable(EditorBrowsableState.Never)]
            [Obsolete("Called by the de-serializer", true)]
            public EnumShim()
            {
            }

            string enumValueString;
            int hash;
            TypeCode typeCode;
            Type type;

            public EnumShim(Enum enumValue)
            {
                this.enumValueString = enumValue.ToString();
                this.hash = enumValue.GetHashCode();
                this.typeCode = enumValue.GetTypeCode();
                this.type = enumValue.GetType();
            }

            public object GetUnderlyingEnum()
            {
                return Enum.Parse(this.type, this.enumValueString);
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                this.enumValueString = info.GetValue<string>("EnumValue");
                this.hash = info.GetValue<int>("Hash");
                this.typeCode = (TypeCode)Enum.Parse(typeof(TypeCode), info.GetValue<string>("TypeCode"));
                this.type = Type.GetType(info.GetValue<string>("TypeAssemblyQualifiedName"));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue("TypeAssemblyQualifiedName", type.AssemblyQualifiedName);
                info.AddValue("EnumValue", this.enumValueString);
                info.AddValue("Hash", this.hash);
                info.AddValue("TypeCode", this.typeCode.ToString());
            }

            public override string ToString()
            {
                return this.enumValueString;
            }

            public override int GetHashCode()
            {
                return this.hash;
            }

            public string ToString(IFormatProvider provider)
            {
                return this.ToString();
            }

            public object ToType(Type conversionType, IFormatProvider provider)
            {
                return Enum.Parse(conversionType, this.enumValueString);
            }

            public TypeCode GetTypeCode()
            {
                return this.typeCode;
            }

            public sbyte ToSByte(IFormatProvider provider)
            {
                return (SByte)Enum.Parse(this.type, this.enumValueString);
            }

            public byte ToByte(IFormatProvider provider)
            {
                return (Byte)Enum.Parse(this.type, this.enumValueString);
            }

            public short ToInt16(IFormatProvider provider)
            {
                return (Int16)Enum.Parse(this.type, this.enumValueString);
            }

            public ushort ToUInt16(IFormatProvider provider)
            {
                return (UInt16)Enum.Parse(this.type, this.enumValueString);
            }

            public int ToInt32(IFormatProvider provider)
            {
                return (Int32)Enum.Parse(this.type, this.enumValueString);
            }

            public uint ToUInt32(IFormatProvider provider)
            {
                return (UInt32)Enum.Parse(this.type, this.enumValueString);
            }

            public long ToInt64(IFormatProvider provider)
            {
                return (Int64)Enum.Parse(this.type, this.enumValueString);
            }

            public ulong ToUInt64(IFormatProvider provider)
            {
                return (UInt64)Enum.Parse(this.type, this.enumValueString);
            }

            public float ToSingle(IFormatProvider provider)
            {
                return (Single)Enum.Parse(this.type, this.enumValueString);
            }

            public double ToDouble(IFormatProvider provider)
            {
                return (Double)Enum.Parse(this.type, this.enumValueString);
            }

            public decimal ToDecimal(IFormatProvider provider)
            {
                return (Decimal)Enum.Parse(this.type, this.enumValueString);
            }

            #region NotImplemented
            public bool ToBoolean(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public char ToChar(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }

            public DateTime ToDateTime(IFormatProvider provider)
            {
                throw new NotImplementedException();
            }
            #endregion NotImplemented
        }
    }
}
