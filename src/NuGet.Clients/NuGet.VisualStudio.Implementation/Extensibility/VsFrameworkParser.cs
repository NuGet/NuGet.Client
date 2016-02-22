// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.Versioning;
using NuGet.Frameworks;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsFrameworkParser))]
    public class VsFrameworkParser : IVsFrameworkParser
    {
        public FrameworkName ParseFrameworkName(string shortOrFullName)
        {
            if (shortOrFullName == null)
            {
                throw new ArgumentNullException(nameof(shortOrFullName));
            }

            var nugetFramework = NuGetFramework.Parse(shortOrFullName);
            return new FrameworkName(nugetFramework.DotNetFrameworkName);
        }
    }
}
