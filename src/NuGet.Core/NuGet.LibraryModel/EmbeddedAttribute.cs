// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis
{
    // Keep linked implementation types out of friend assemblies' compiler lookup.
    [Embedded]
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class EmbeddedAttribute : Attribute
    {
    }
}
