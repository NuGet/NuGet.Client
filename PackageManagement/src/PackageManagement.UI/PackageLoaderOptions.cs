// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    internal class PackageLoaderOption
    {
        public PackageLoaderOption(
            Filter filter,
            bool includePrerelease)
        {
            Filter = filter;
            IncludePrerelease = includePrerelease;

            switch (filter)
            {
                case Filter.All:
                    PageSize = 25;
                    break;

                case Filter.UpdatesAvailable:
                case Filter.Installed:
                default:
                    PageSize = 100;
                    break;
            }
        }

        public Filter Filter { get; }

        public bool IncludePrerelease { get; }

        public int PageSize { get; }
    }
}
