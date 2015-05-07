// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.VisualStudio
{
    // this scriptPackage is a package inferface for script executor
    // it provides an IPackage like interface to make sure all install.ps scripts which depend on IPackage keep working
    public class ScriptPackage : IScriptPackage
    {
        private string _id;
        private string _version;

        public ScriptPackage(string id, string version)
        {
            _id = id;
            _version = version;
        }

        public string Id
        {
            get { return _id; }
        }

        public string Version
        {
            get { return _version; }
        }
    }
}
