// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager.Test
{
    internal class VsReferenceItems : VsItemList<IVsReferenceItem>, IVsReferenceItems
    {
        public VsReferenceItems() : base() { }

        public VsReferenceItems(IEnumerable<IVsReferenceItem> collection) : base(collection) { }

        protected override String GetKeyForItem(IVsReferenceItem value) => value.Name;
    }
}
