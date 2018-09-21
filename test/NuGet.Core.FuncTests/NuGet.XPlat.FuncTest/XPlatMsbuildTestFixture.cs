// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatMsbuildTestFixture : IDisposable
    {
        internal readonly string _dotnetCli = DotnetCliUtil.GetDotnetCli();

        public XPlatMsbuildTestFixture()
        {
            var cliDirectory = Directory.GetParent(_dotnetCli);
            var msBuildSdksPath = Path.Combine(Directory.GetDirectories(Path.Combine(cliDirectory.FullName, "sdk")).First(), "Sdks");
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", msBuildSdksPath);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", null);
        }
    }
}