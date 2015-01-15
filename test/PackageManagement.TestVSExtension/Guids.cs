// Guids.cs
// MUST match guids.h
using System;

namespace NuGet.PackageManagement_TestVSExtension
{
    static class GuidList
    {
        public const string guidPackageManagement_TestVSExtensionPkgString = "69e0f333-f826-42c2-8c3f-1484cb237a0d";
        public const string guidPackageManagement_TestVSExtensionCmdSetString = "7e9c7393-b7d6-4f2e-b477-10b68b10705e";
        public const string guidToolWindowPersistanceString = "9ca833a6-8d1e-48ae-bd42-db97877829ae";

        public static readonly Guid guidPackageManagement_TestVSExtensionCmdSet = new Guid(guidPackageManagement_TestVSExtensionCmdSetString);
    };
}