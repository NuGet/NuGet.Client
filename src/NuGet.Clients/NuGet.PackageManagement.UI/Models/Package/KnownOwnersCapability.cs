// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

# nullable enable

using System;
using System.Collections.Generic;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class KnownOwnersCapability : IKnownOwnersCapable
    {
        public KnownOwnersCapability(IReadOnlyList<string> ownersList, IOwnerDetailsUriService? ownerDetailsUriService)
        {
            KnownOwners = LoadKnownOwners(ownersList, ownerDetailsUriService);
        }

        public IReadOnlyList<KnownOwner>? KnownOwners { get; }

        private static IReadOnlyList<KnownOwner>? LoadKnownOwners(IReadOnlyList<string> ownersList, IOwnerDetailsUriService? ownerDetailsUriService)
        {
            if (ownerDetailsUriService is null
                || !ownerDetailsUriService.SupportsKnownOwners)
            {
                return null;
            }

            if (ownersList is null || ownersList.Count == 0)
            {
                return Array.Empty<KnownOwner>();
            }

            List<KnownOwner> knownOwners = new(capacity: ownersList.Count);

            foreach (string owner in ownersList)
            {
                Uri ownerDetailsUrl = ownerDetailsUriService.GetOwnerDetailsUri(owner);
                KnownOwner knownOwner = new(owner, ownerDetailsUrl);
                knownOwners.Add(knownOwner);
            }

            return knownOwners;
        }
    }
}
