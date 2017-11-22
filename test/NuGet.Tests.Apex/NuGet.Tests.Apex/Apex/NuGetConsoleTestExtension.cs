// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Console.TestContract;

namespace NuGet.Tests.Apex
{
    public class NuGetConsoleTestExtension : NuGetBaseTestExtension<object, NuGetConsoleTestExtensionVerifier>
    {
        private ApexTestConsole _pmConsole;
        private TimeSpan _timeout = TimeSpan.FromMinutes(5);

        public string _projectName { get; set; }

        public NuGetConsoleTestExtension(ApexTestConsole console, string projectName)
        {
            _pmConsole = console;
            _projectName = projectName;
        }

        public bool InstallPackageFromPMC(string packageId, string version)
        {
            var command = $"Install-Package {packageId} -ProjectName {_projectName} -Version {version} ";
            return _pmConsole.WaitForActionComplete(() => _pmConsole.RunCommand(command), _timeout);
        }

        public bool InstallPackageFromPMC(string packageId, string version, string source)
        {
            var command = $"Install-Package {packageId} -ProjectName {_projectName} -Version {version} -Source {source}";
            return _pmConsole.WaitForActionComplete(() => _pmConsole.RunCommand(command), _timeout);
        }

        public bool UninstallPackageFromPMC(string packageId)
        {
            var command = $"Uninstall-Package {packageId} -ProjectName {_projectName}";
            return _pmConsole.WaitForActionComplete(() => _pmConsole.RunCommand(command), _timeout);
        }

        public bool UpdatePackageFromPMC(string packageId, string version)
        {
            var command = $"Update-Package {packageId} -ProjectName {_projectName} -Version {version}";
            return _pmConsole.WaitForActionComplete(() => _pmConsole.RunCommand(command), _timeout);
        }

        public bool IsPackageInstalled(string packageId, string version)
        {
            return _pmConsole.IsPackageInstalled(_projectName, packageId, version);
        }

        public void Clear()
        {
            _pmConsole.Clear();
        }
    }
}
