// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel.Test
{
    public static class PackageSpecTestUtility
    {
        public static PackageSpec RoundTrip(this PackageSpec spec)
        {
            var writer = new JsonObjectWriter();
            PackageSpecWriter.Write(spec, writer);
            var json = writer.GetJObject();

            return JsonPackageSpecReader.GetPackageSpec(json);
        }

        public static PackageSpec GetSpec()
        {
            return GetSpec("netcoreapp2.0");
        }

        public static PackageSpec GetSpec(params NuGetFramework[] frameworks)
        {
            var tfis = new List<TargetFrameworkInformation>(
                frameworks.Select(e => new TargetFrameworkInformation()
                {
                    FrameworkName = e
                }));

            return new PackageSpec(tfis);
        }

        public static PackageSpec GetSpec(params string[] frameworks)
        {
            return GetSpec(frameworks.Select(NuGetFramework.Parse).ToArray());
        }
    }
}
