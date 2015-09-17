// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.PowerShellGet
{
    public class PowerShellSearchPackage : PackageIdentity
    {
        private readonly ServerPackageMetadata _serverPackage;
        private readonly PSPackageMetadata _powershellMetadata;

        public PowerShellSearchPackage(ServerPackageMetadata serverPackage, PSPackageMetadata powershellMetadata)
            : base(serverPackage.Id, serverPackage.Version)
        {
            _serverPackage = serverPackage;
            _powershellMetadata = powershellMetadata;
        }

        /// <summary>
        /// General nupkg info from NuGet search
        /// </summary>
        public ServerPackageMetadata ServerPackage
        {
            get { return _serverPackage; }
        }

        /// <summary>
        /// Powershell metadata from search
        /// </summary>
        public PSPackageMetadata PowerShellMetadata
        {
            get { return _powershellMetadata; }
        }
    }
}
