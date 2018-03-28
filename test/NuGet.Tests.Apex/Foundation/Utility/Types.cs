using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet.Tests.Foundation.Extensions;

namespace NuGet.Tests.Foundation.Utility
{
    public static class Types
    {
        /// <summary>
        /// Gets the attributes of the specified type, if any
        /// </summary>
        /// <param name="inherit">Walks the inheritance chain to look for attributes</param>
        public static IEnumerable<T> GetAttributes<T>(this object target, bool inherit = false) where T : Attribute
        {
            Type targetType = target as Type;
            if (targetType == null) { targetType = target.GetType(); }

            foreach (object attribute in targetType.GetCustomAttributes(typeof(T), inherit))
            {
                yield return (T)attribute;
            }
        }

        /// <summary>
        /// Gets the attributes of the specified type from generic type arguments, if any
        /// </summary>
        /// <param name="inherit">Walks the inheritance chain to look for attributes</param>
        public static IEnumerable<T> GetAttributesFromGenericTypeArguments<T>(this object target, bool inherit = false) where T : Attribute
        {
            Type targetType = target.GetType();
            foreach (Type interfaceType in targetType.GetInterfaces())
            {
                if (interfaceType.IsGenericType)
                {
                    foreach (Type typeArgument in interfaceType.GenericTypeArguments)
                    {
                        foreach (object attribute in typeArgument.GetCustomAttributes(typeof(T), inherit))
                        {
                            yield return (T)attribute;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to invoke the finalizer for testing purposes.
        /// </summary>
        public static void InvokeFinalizer(object target)
        {
            if (target == null) { throw new ArgumentNullException("target"); }

            var method = target.GetType().GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, null);
        }

        /// <summary>
        /// Gets the requested private property of the given type.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the requested property isn't private, or doesn't exist</exception>
        /// <exception cref="System.InvalidCastException">If the requested property is not of the given type.</exception>
        /// <exception cref="System.ArgumentNullException">If <paramref name="target"/> or <paramref name="propertyName"/> is null.</exception>
        /// <param name="target">The instance to get the property from.</param>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        public static T GetPrivateProperty<T>(object target, string propertyName)
        {
            PropertyInfo info = Types.GetPrivatePropertyInfo(target, propertyName);
            return (T)info.GetValue(target);
        }

        private static PropertyInfo GetPrivatePropertyInfo(object target, string propertyName)
        {
            if (target == null) { throw new ArgumentNullException("target"); }
            if (String.IsNullOrWhiteSpace(propertyName)) { throw new ArgumentNullException("propertyName"); }

            Type targetType = target.GetType();
            PropertyInfo property = targetType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null) { throw new InvalidOperationException(); }
            return property;
        }

        /// <summary>
        /// Gets the requested private static field of the given type.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the requested field isn't private, or doesn't exist</exception>
        /// <exception cref="System.InvalidCastException">If the requested field is not of the given type.</exception>
        /// <exception cref="System.ArgumentNullException">If <paramref name="targetType"/> or <paramref name="fieldName"/> is null.</exception>
        /// <param name="targetType">The type to get the static field from.</param>
        /// <param name="fieldName">The name of the field</param>
        public static T GetPrivateStaticField<T>(Type targetType, string fieldName)
        {
            if (targetType == null) { throw new ArgumentNullException("targetType"); }
            if (String.IsNullOrWhiteSpace(fieldName)) { throw new ArgumentNullException("fieldName"); }

            FieldInfo field = targetType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) { throw new InvalidOperationException(); }
            return (T)field.GetValue(null);
        }

        /// <summary>
        /// Converts the source object to the desired type if possible.  Returns default of T if it cannot.  Nullable tolerant.
        /// </summary>
        /// <remarks>
        /// This method tries to be as flexible as possible, allowing conversion to nullable for source primitives (including enums).
        /// This allows you to ask for nullable of int for an underlying int source object- this way you can know
        /// the conversion failed if you care to.  It also also allows you to ask for the primitive type for a source
        /// nullable type if you don't care about conversion success.
        /// </remarks>
        public static T ConvertType<T>(object source)
        {
            if (source == null) { return default(T); }

            Type type = typeof(T);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // For nullable types we want to use the underlying type for the conversion
                type = type.GetGenericArguments()[0];
            }

            if (type.IsAssignableFrom(source.GetType()))
            {
                // Assignable- cast and return
                return (T)source;
            }

            if (type.IsPrimitive)
            {
                // Primitive type, use Convert
                try
                {
                    return (T)Convert.ChangeType(source, type, CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    if (e is InvalidCastException
                        || e is ArgumentNullException
                        || e is FormatException
                        || e is OverflowException)
                    {
                        // One special case here- conceptually converting a number to a bool makes a lot of sense as "0" == false for *all* primitive types, but
                        // converting a string or char representation of a number will fail (only "true" and "false" are valid).
                        // If we have a source object that can be converted to double, use that for the conversion (could potentially use decimal, but
                        // double seems to be more likely to have a valid converter)
                        if (type == typeof(bool))
                        {
                            double? numericBool = Types.ConvertType<double?>(source);
                            if (numericBool.HasValue)
                            {
                                return (T)Convert.ChangeType(numericBool.Value, type, CultureInfo.InvariantCulture);
                            }
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                // Not a primitive type that was requested- give the default TypeConverter a try
                try
                {
                    TypeConverter typeConverter = TypeDescriptor.GetConverter(type);
                    if (typeConverter != null && typeConverter.CanConvertFrom(source.GetType()))
                    {
                        return (T)typeConverter.ConvertFrom(source);
                    }
                    else
                    {
                        if (type.IsEnum && source != null)
                        {
                            try
                            {
                                T convertedEnum = (T)Enum.Parse(type, source.ToString(), ignoreCase: true);
                                if (Enum.IsDefined(type, convertedEnum))
                                {
                                    return convertedEnum;
                                }
                                else
                                {
                                    if (default(T) == null)
                                    {
                                        // We were asked for a nullable enum, allow a null
                                        return default(T);
                                    }

                                    Array values = Enum.GetValues(type);
                                    if (values.Length > 0)
                                    {
                                        return (T)values.GetValue(0);
                                    }

                                    return default(T);
                                }
                            }
                            catch (Exception e)
                            {
                                if (!(e is InvalidCastException
                                    || e is ArgumentNullException
                                    || e is FormatException
                                    || e is OverflowException))
                                {
                                    throw;
                                }
                            }
                        }
                        Debug.WriteLine(String.Format(CultureInfo.InvariantCulture, "No converter found for type '{0}'", type));
                    }
                }
                catch (Exception e)
                {
                    if (!(e is InvalidCastException
                        || e is ArgumentNullException
                        || e is FormatException
                        || e is OverflowException
                        || e is NotSupportedException))
                    {
                        throw;
                    }
                }
            }

            // Did we ask for a string?  Try it if the TypeConverter failed first.
            if (type == typeof(string))
            {
                return (T)(object)source.ToString();
            }

            return default(T);
        }

        /// <summary>
        /// Dump the state of an object. Dumps all public, private, instance, and static fields recursively.
        /// </summary>
        public static string DumpState<T>(T value, int maxDepth = 5)
        {
            StringWriter sw = new StringWriter();
            Types.DumpState<T>(value, sw);
            return sw.ToString();
        }

        /// <summary>
        /// Dump the state of an object. Dumps all public, private, instance, and static fields recursively.
        /// </summary>
        public static void DumpState<T>(T value, TextWriter writer, int maxDepth = 5)
        {
            Types.DumpInternal(0, null, typeof(T), value, writer, maxDepth);
        }

        /// <summary>
        /// Dump the state of a type. Dumps all public and private static fields recursively.
        /// </summary>
        public static string DumpState(Type type, int maxDepth = 5)
        {
            StringWriter sw = new StringWriter();
            Types.DumpState(type, sw);
            return sw.ToString();
        }

        /// <summary>
        /// Dump the state of a type. Dumps all public and private static fields recursively.
        /// </summary>
        public static void DumpState(Type type, TextWriter writer, int maxDepth = 5)
        {
            Types.DumpInternal(0, null, type, null, writer, maxDepth);
        }

        private static void DumpInternal(int indentLevel, string name, Type type, object value, TextWriter writer, int maxDepth)
        {
            Types.DumpRecursive(indentLevel, name, type, value, writer, new List<object>(), new List<FieldInfo>(), maxDepth);
        }

        private static void DumpRecursive(int indentLevel, string name, Type type, object value, TextWriter writer, List<object> seenObjects, List<FieldInfo> seenStaticFields, int maxDepth)
        {
            if (indentLevel == maxDepth)
            {
                writer.WriteValue(indentLevel, name, type.Name, "<Max depth reached>");
                return;
            }

            // Prefer getting the type off of the instance, T could be a boxed value type or upcast/downcast
            type = value == null ? type : value.GetType();

            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid))
            {
                // "Intrinsic" types, or a simple known value type
                writer.WriteValue(indentLevel, name, type.Name, value);
                return;
            }

            string typeName = type.UndecoratedName();
            int objectId = -1;

            if (!type.IsValueType && value != null)
            {
                // Don't want to end up in an endless loop with circular references
                int priorObjectId = seenObjects.FindIndex(o => object.ReferenceEquals(o, value));

                if (priorObjectId != -1)
                {
                    writer.WriteValue(indentLevel, name, typeName, String.Format("<class [see #{0}]>", priorObjectId));
                    return;
                }

                objectId = seenObjects.Count;
                seenObjects.Add(value);
            }

            if (type.IsArray)
            {
                int arrayDimensions = type.GetArrayRank();
                writer.WriteValue(indentLevel, name, typeName, String.Format("<array [#{0}]>", objectId));

                Array array = (Array)(object)value;
                array.ForEachIndex((element, indicies) =>
                {
                    string index = String.Format("[{0}]", String.Join(",", indicies));
                    Types.DumpRecursive(indentLevel + 1, index, type.GetElementType(), element, writer, seenObjects, seenStaticFields, maxDepth);
                });
            }
            else
            {
                // Not an array, dump and go for fields
                if (value == null)
                {
                    writer.WriteValue(indentLevel, name, typeName, type.IsValueType ? "<null struct>" : "<null class>");
                }
                else
                {
                    writer.WriteValue(indentLevel, name, typeName, type.IsValueType ? "<struct>" : String.Format("<class [#{0}]>", objectId));
                }

                var staticFields =
                (
                    from field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    where !seenStaticFields.Contains(field)
                    select field
                ).ToArray();

                var instanceFields =
                (
                    from field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    select field
                ).ToArray();

                seenStaticFields.AddRange(staticFields);

                foreach (var field in staticFields.Concat(instanceFields))
                {
                    object fieldValue = (field.IsStatic || value != null) ? field.GetValue(value) : "<instance>";
                    string fieldName = field.UndecoratedName();

                    if (fieldValue == null)
                    {
                        writer.WriteValue(indentLevel + 1, fieldName, typeName, "<null>");
                    }
                    else
                    {
                        Types.DumpRecursive(indentLevel + 1, fieldName, field.FieldType, fieldValue, writer, seenObjects, seenStaticFields, maxDepth);
                    }
                }
            }
        }

        private static string UndecoratedName(this FieldInfo fieldInfo)
        {
            string fieldName = fieldInfo.Name;
            if (fieldName.StartsWith("<"))
            {
                fieldName = String.Format("{0} (Backing)", fieldName.Split('<', '>')[1]);
            }

            return fieldName;
        }

        private static void WriteValue(this TextWriter writer, int indentLevel, string name, string type, object value)
        {
            string indent = new string(' ', indentLevel * 3);

            if (value == null)
            {
                value = "<null>";
            }

            if (name == null)
            {
                writer.WriteLine("{0}[{1}] = {2}", indent, type, value);
            }
            else
            {
                writer.WriteLine("{0}{1} [{2}] = {3}", indent, name, type, value);
            }
        }

    }
}
