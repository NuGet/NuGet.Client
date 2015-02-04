// Guids.cs
// MUST match guids.h
using System;

namespace NuGet.VisualStudioAPI_TestExtension
{
    static class GuidList
    {
        public const string guidVisualStudioAPI_TestExtensionPkgString = "9cabf172-fc18-4ffb-901e-ad0b8c41d19c";
        public const string guidVisualStudioAPI_TestExtensionCmdSetString = "40ab6a66-6483-4f80-ba7b-65398402d9d7";

        public static readonly Guid guidVisualStudioAPI_TestExtensionCmdSet = new Guid(guidVisualStudioAPI_TestExtensionCmdSetString);
    };
}