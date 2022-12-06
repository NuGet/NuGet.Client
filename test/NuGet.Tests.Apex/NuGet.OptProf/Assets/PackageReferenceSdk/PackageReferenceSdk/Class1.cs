// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace PackageReferenceSdk
{
    public class Class1
    {
        internal JObject JObject { get; }

        internal Class1()
        {
            JObject = JObject.Parse("{}");
        }
    }
}
