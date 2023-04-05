// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Protocol.Model
{
    internal class IgnoreCaseDictionary<TValue> : Dictionary<string, TValue>
    {
        public IgnoreCaseDictionary()
            : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }
    }
}
