using System;

namespace NuGetConsole
{
    internal static class UtilityMethods
    {
        public static void ThrowIfArgumentNull<T>(T arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException("arg");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Performance",
            "CA1811:AvoidUncalledPrivateCode",
            Justification = "This class is shared with another project, and the other project does call this method.")]
        public static void ThrowIfArgumentNullOrEmpty(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                throw new ArgumentException("Invalid argument", "arg");
            }
        }
    }
}
