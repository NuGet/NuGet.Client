// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.Services.Profile;


namespace NuGet.Tests.Apex.NuGetEndToEndTests
{
    internal class WebSiteProjectItems
    {
        public List<WebSiteProjectItem> InitProjectItems()
        {
            List<WebSiteProjectItem> _websiteProjectItems = new List<WebSiteProjectItem>();
            _websiteProjectItems.Add(new WebSiteProjectItem() { Template=ProjectTemplate.WebSiteEmpty, ProjectName="WebSiteEmpty", TargetFramework=ProjectTargetFramework.V48, PackageName="log4net", PackageVersion="2.0.13" });
            _websiteProjectItems.Add(new WebSiteProjectItem() { Template=ProjectTemplate.WebSite, ProjectName="WebSite", TargetFramework=ProjectTargetFramework.V48, PackageName="Log4Net.Async", PackageVersion="2.0.3" });
            _websiteProjectItems.Add(new WebSiteProjectItem() { Template=ProjectTemplate.WebSiteWCF, ProjectName="WebSiteWCF", TargetFramework=ProjectTargetFramework.V48, PackageName="log4net.Ext.Json", PackageVersion="2.0.9.1" });
            _websiteProjectItems.Add(new WebSiteProjectItem() { Template=ProjectTemplate.WebSiteDynamicDataEntityFramework, ProjectName="WebSiteEntities", TargetFramework=ProjectTargetFramework.V48, PackageName="log4net", PackageVersion="2.0.13" });
            _websiteProjectItems.Add(new WebSiteProjectItem() { Template=ProjectTemplate.WebSiteDynamicDataLinqToSql, ProjectName="WebSiteLinq", TargetFramework=ProjectTargetFramework.V45, PackageName="Log4Net.Async", PackageVersion="2.0.3" });
            return _websiteProjectItems;
        }

    }

    internal class WebSiteProjectItem
    {
        public ProjectTemplate Template { get; set; }
        public string ProjectName { get; set; }
        public ProjectTargetFramework TargetFramework { get; set; }
        public string PackageName { get; set; }
        public string PackageVersion { get; set; }

    }
}
