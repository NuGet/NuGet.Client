// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Framework specific assets for a SimpleTestProjectContext
    /// </summary>
    public class SimpleTestProjectFrameworkContext
    {
        /// <summary>
        /// Target framework
        /// </summary>
        public NuGetFramework Framework { get; }

        /// <summary>
        /// Package dependencies.
        /// </summary>
        public List<SimpleTestPackageContext> PackageReferences { get; set; } = new List<SimpleTestPackageContext>();

        /// <summary>
        /// Project dependencies.
        /// </summary>
        public List<SimpleTestProjectContext> ProjectReferences { get; set; } = new List<SimpleTestProjectContext>();

        /// <summary>
        /// Framework specific properties.
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Project framework assembly references.
        /// </summary>
        public List<string> FrameworkAssemblyReferences { get; set; } = new List<string>();

        public SimpleTestProjectFrameworkContext()
            : this(NuGetFramework.Parse("net461"), Enumerable.Empty<SimpleTestPackageContext>(), Enumerable.Empty<SimpleTestProjectContext>())
        {
        }

        public SimpleTestProjectFrameworkContext(NuGetFramework framework)
            : this(framework, Enumerable.Empty<SimpleTestPackageContext>(), Enumerable.Empty<SimpleTestProjectContext>())
        {
        }

        public SimpleTestProjectFrameworkContext(
            NuGetFramework framework,
            IEnumerable<SimpleTestPackageContext> packageReferences)
            : this(framework, packageReferences, Enumerable.Empty<SimpleTestProjectContext>())
        {
        }

        public SimpleTestProjectFrameworkContext(
            NuGetFramework framework,
            IEnumerable<SimpleTestProjectContext> projectReferences)
            : this(framework, Enumerable.Empty<SimpleTestPackageContext>(), projectReferences)
        {
        }

        public SimpleTestProjectFrameworkContext(
            NuGetFramework framework,
            IEnumerable<SimpleTestPackageContext> packageReferences,
            IEnumerable<SimpleTestProjectContext> projectReferences)
        {
            Framework = framework;
            PackageReferences.AddRange(packageReferences);
            ProjectReferences.AddRange(projectReferences);
        }
    }
}
