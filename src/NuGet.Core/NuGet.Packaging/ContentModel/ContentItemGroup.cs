﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.ContentModel
{
    public class ContentItemGroup
    {
        public ContentItemGroup()
        {
            Properties = new Dictionary<string, object>();
            Items = new List<ContentItem>();
        }

        public IDictionary<string, object> Properties { get; }

        public IList<ContentItem> Items { get; }
    }
}
