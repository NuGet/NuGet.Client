// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Media;
using NuGet.PackageManagement;
using NuGet.VisualStudio;

namespace NuGetConsole.Host
{
    /// <summary>
    /// This host is used when PowerShell 2.0 runtime is not installed in the system. It's basically a no-op host.
    /// </summary>
    internal class UnsupportedHost : IHost
    {
        public bool IsCommandEnabled
        {
            get { return false; }
        }

        public void Initialize(IConsole console)
        {
            // display the error message at the beginning
            console.Write(PowerShell.Resources.Host_PSNotInstalled, Colors.Red, null);
        }

        public string Prompt
        {
            get { return String.Empty; }
        }

        public bool Execute(IConsole console, string command, object[] inputs)
        {
            return false;
        }

        public void Abort()
        {
        }

        public string ActivePackageSource
        {
            get { return String.Empty; }
            set { }
        }

        public string[] GetPackageSources()
        {
            return new string[0];
        }

        public string DefaultProject
        {
            get { return String.Empty; }
        }

        public void SetDefaultProjectIndex(int index)
        {
        }

        public string[] GetAvailableProjects()
        {
            return new string[0];
        }

        public void SetDefaultRunspace()
        {
        }

        public PackageManagementContext PackageManagementContext
        {
            get { return null; }
            set { }
        }
    }
}
