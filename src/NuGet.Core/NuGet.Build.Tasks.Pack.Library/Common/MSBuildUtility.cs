﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.Build.Tasks.Pack
{
    internal static class MSBuildUtility
    {
        public static IMSBuildItem WrapMSBuildItem(ITaskItem item)
        {
            if (item == null)
            {
                return null;
            }

            return new MSBuildTaskItem(item);
        }

        public static IMSBuildItem[] WrapMSBuildItem(IEnumerable<ITaskItem> items)
        {
            if (items == null)
            {
                return new IMSBuildItem[0];
            }

            return items
                .Select(WrapMSBuildItem)
                .Where(item => item != null)
                .ToArray();
        }
    }
}