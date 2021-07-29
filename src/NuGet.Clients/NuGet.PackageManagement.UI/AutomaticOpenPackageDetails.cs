// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.UI
{
    
    public class AutomaticOpenPackageDetails
    {
        private string _packageName;
        private string _versionNumber;

        public AutomaticOpenPackageDetails(string packageName, string versionNumber)
        {

        }

        public string PackageName
        {
            get
            {
                return _packageName;
            }

            private set
            {
                _packageName = value;
            }
        }

        public string VersionNumber
        {
            get
            {
                return _versionNumber;
            }

            private set
            {
                _versionNumber = value;
            }
        }

        public void Search()
        {

        }

        private void AdvancedSearch()
        {

        }

        public void OpenDetails()
        {

        }
    }
}
