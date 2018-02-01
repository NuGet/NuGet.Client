using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetClient.Test.Foundation.Extensions
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Returns the full name of the type without namespace and type arguments (in the case of generics)
        /// </summary>
        public static string FullNameWithoutNamespace(this Type type)
        {
            // We don't want the full type specs on the end for generic types, so pull from generic type definition for generics.
            // E.g., we want ConsoleApplication1.Foo`2+Bar`1, *not*
            // ConsoleApplication1.Foo`2+Bar`1[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.UInt32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]
            string fullName = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
            return String.IsNullOrEmpty(type.Namespace)
                ? fullName
                : fullName.Substring(type.Namespace.Length + 1);
        }

        /// <summary>
        /// Returns the name of the type without namespace and with the generic arguments filled in and formatted (e.g. List`1 becomes List<String>)
        /// </summary>
        public static string UndecoratedName(this Type type)
        {
            string typeName = type.FullNameWithoutNamespace();

            if (!type.IsGenericType)
            {
                return typeName;
            }

            // Split the specification up and fill out the generic type arguments
            Queue<Type> genericArguments = new Queue<Type>(type.GetGenericArguments());

            string[] typeSegments = typeName.Split('+');

            for (int i = 0; i < typeSegments.Length; i++)
            {
                string[] args = typeSegments[i].Split('`');

                // In nested classes, not all of the classes will have a generic argument.
                if (args.Length == 2)
                {
                    int argumentCount = int.Parse(args[1]);
                    typeSegments[i] = String.Format(
                        "{0}<{1}>",
                        args[0],
                        String.Join(",", genericArguments.Dequeue(argumentCount).Select(UndecoratedName)));
                }
            }

            return String.Join("+", typeSegments);
        }
    }
}
