// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Commands;
using NuGet.Packaging;

namespace NuGet.Build.Tasks.Pack
{
    public interface IPackTaskLogic
    {
        PackageBuilder GetPackageBuilder(IPackTaskRequest<IMSBuildItem> request);

        PackArgs GetPackArgs(IPackTaskRequest<IMSBuildItem> request);

        PackCommandRunner GetPackCommandRunner(
            IPackTaskRequest<IMSBuildItem> request,
            PackArgs packArgs,
            PackageBuilder packageBuilder);

        void BuildPackage(PackCommandRunner runner);
    }
}