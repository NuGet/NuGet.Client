// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Test.Utility
{
    public static class TestSources
    {
        public const string ConfigFile = "NuGet.Core.FuncTests.Config";
        public const string NuGetV2Uri = @"https://www.nuget.org/api/v2";

        public const string Artifactory = nameof(Artifactory);
        public const string Klondike = nameof(Klondike);
        public const string MyGet = nameof(MyGet);
        public const string Nexus = nameof(Nexus);
        public const string NuGetServer = nameof(NuGetServer);
        public const string ProGet = nameof(ProGet);
        public const string TeamCity = nameof(TeamCity);
        public const string VSTS = nameof(VSTS);

        public static string GetConfigFileRoot()
        {
            var fullPath = Environment.GetEnvironmentVariable("NuGet_Core_FuncTests_Config");
            return string.IsNullOrEmpty(fullPath) ? NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory) : fullPath;
        }
    }
}
