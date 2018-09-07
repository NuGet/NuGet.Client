// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VcxProject
    {
        private readonly XDocument vcxFile;

        public VcxProject(string fullname)
        {
            vcxFile = XDocument.Load(fullname);
        }

        public bool HasClrSupport()
        {
            var elements = vcxFile.Descendants().Where(x => x.Name.LocalName == "PropertyGroup");
            var actualPropertyGroups =
                elements.Where(x => x.Attribute("Label") != null && x.Attribute("Label").Value == "Configuration");
            var clritems = actualPropertyGroups.Elements().Where(e => e.Name.LocalName == "CLRSupport");
            var overrideitems = actualPropertyGroups.Elements().Where(e => e.Name.LocalName == "UseNativeNuGet");
            if (overrideitems.Any())
            {
                var useNativeNuget = overrideitems.First().Value;
                if (string.Equals(useNativeNuget, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            if (clritems.Any())
            {
                var clr = clritems.First();
                return string.Equals(clr.Value, "true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}
