using System;
using System.IO;
using System.Reflection;

namespace NuGet.Test.Utility
{
    public static class ResourceTestUtility
    {
        public static string GetResource(string name, Type type)
        {
            using (var reader = new StreamReader(type.GetTypeInfo().Assembly.GetManifestResourceStream(name)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
