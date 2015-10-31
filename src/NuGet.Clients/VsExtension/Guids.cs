// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetVSExtension
{
    internal static class GuidList
    {
        public const string guidNuGetPkgString = "5fcc8577-4feb-4d04-ad72-d6c629b083cc";
        public const string guidNuGetVSEventsPackagePkgString = "38ebd926-b8b6-44e9-952d-1cfd38c84209";

        public const string guidNuGetConsoleCmdSetString = "1E8A55F6-C18D-407F-91C8-94B02AE1CED6";
        public const string guidNuGetDialogCmdSetString = "25fd982b-8cae-4cbd-a440-e03ffccde106";
        public const string guidNuGetToolsGroupString = "C0D88179-5D25-4982-BFE6-EC5FD59AC103";
        public const string guidNuGetDebugConsoleCmdSetString = "DDC61543-6CA7-4A6F-A5B7-984BE723C52F";

        // any project system that wants to load NuGet when its project opens needs to activate a UI context with this GUID
        public const string guidAutoLoadNuGetString = "65B1D035-27A5-4BBA-BAB9-5F61C1E2BC4A";

        public static readonly Guid guidNuGetConsoleCmdSet = new Guid(guidNuGetConsoleCmdSetString);
        public static readonly Guid guidNuGetDialogCmdSet = new Guid(guidNuGetDialogCmdSetString);
        public static readonly Guid guidNuGetToolsGroupCmdSet = new Guid(guidNuGetToolsGroupString);
        public static readonly Guid guidNuGetDebugConsoleCmdSet = new Guid(guidNuGetDebugConsoleCmdSetString);
    }
}
