// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.VisualStudio.Implementation.Resources
{
    internal static class VsResourcesFormat
    {
        public static string PropertyCannotBeNull(string property)
        {
            return string.Format(VsResources.PropertyCannotBeNull, property);
        }
    }
}
