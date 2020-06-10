// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.VS.Tree.Dependencies.AttachedCollections;
using NuGet.VisualStudio.SolutionExplorer.Models;

namespace NuGet.VisualStudio.SolutionExplorer
{
    [Export(typeof(IRelation))]
    internal sealed class PackageToFrameworkAssemblyGroupRelation : RelationBase<PackageReferenceItem, PackageAssemblyGroupItem>
    {
        protected override bool HasContainedItems(PackageReferenceItem parent) => parent.Library.FrameworkAssemblies.Length != 0;

        protected override void UpdateContainsCollection(PackageReferenceItem parent, AggregateContainsRelationCollectionSpan span)
        {
            span.UpdateContainsItems(
                parent.Library.FrameworkAssemblies.Length == 0 ? Array.Empty<AssetsFileTargetLibrary>() : new[] { parent.Library },
                (library, item) => 0,
                (library, item) => false,
                library => new PackageAssemblyGroupItem(parent.Target, library, PackageAssemblyGroupType.Framework));
        }

        protected override IEnumerable<PackageReferenceItem>? CreateContainedByItems(PackageAssemblyGroupItem child)
        {
            if (child.GroupType == PackageAssemblyGroupType.Framework)
            {
                yield return new PackageReferenceItem(child.Target, child.Library);
            }
        }
    }
}
