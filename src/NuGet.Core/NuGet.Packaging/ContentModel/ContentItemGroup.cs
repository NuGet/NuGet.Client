// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.ContentModel
{
    [DebuggerDisplay("Items: {Items.Count}, Properties: {Properties.Count}")]
    public class ContentItemGroup
    {
        public ContentItemGroup()
        {
            Properties = new Dictionary<string, object>();
            Items = new List<ContentItem>();
        }

#pragma warning disable RS0016 // Add public types and members to the declared API
        public ContentItemGroup(IDictionary<string, object> properties, IList<ContentItem> items)
#pragma warning restore RS0016 // Add public types and members to the declared API
        {
            Properties = properties;
            Items = items;
        }

        public IDictionary<string, object> Properties { get; }

        public IList<ContentItem> Items { get; }
    }
}
