// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchArgs
    {
        private readonly int _defaultSkip = 0;
        private readonly int _defaultTake = 20;
        public List<string> Sources { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public bool Prerelease { get; set; }
        public bool ExactMatch { get; set; }
        public bool Interactive { get; set; }
        public ILogger Logger { get; set; }
        public string SearchTerm { get; set; }
        public PackageSearchCommandFormat Format { get; set; }

        public PackageSearchArgs(string skip, string take, string format)
        {
            Skip = VerifyInt(skip, _defaultSkip);
            Take = VerifyInt(take, _defaultTake);
            Format = VerifyFormat(format);
        }

        private PackageSearchCommandFormat VerifyFormat(string format)
        {
            if (string.IsNullOrEmpty(format) || string.Equals(format, nameof(PackageSearchCommandFormat.Table), StringComparison.CurrentCultureIgnoreCase))
            {
                return PackageSearchCommandFormat.Table;
            }

            if (string.Equals(format, nameof(PackageSearchCommandFormat.Json), StringComparison.CurrentCultureIgnoreCase))
            {
                return PackageSearchCommandFormat.Json;
            }

            if (string.Equals(format, nameof(PackageSearchCommandFormat.List), StringComparison.CurrentCultureIgnoreCase))
            {
                return PackageSearchCommandFormat.List;
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidPackageSearchFormat, format));
        }

        public PackageSearchArgs(string format)
        {
            Format = VerifyFormat(format);
        }

        public int VerifyInt(string number, int defaultValue)
        {
            if (string.IsNullOrEmpty(number))
            {
                return defaultValue;
            }

            if (int.TryParse(number, out int verifiedNumber))
            {
                return verifiedNumber;
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_invalid_number, number));
        }
    }
}
