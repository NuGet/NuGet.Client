// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal interface ITableFormatStrategy
    {
        Table CreateTable(IEnumerable<IPackageSearchMetadata> results, bool exactMatch);
    }
}
