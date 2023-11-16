// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchArgs
    {
        private const int DefaultSkip = 0;
        private const int DefaultTake = 20;
        public List<string> Sources { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public bool Prerelease { get; set; }
        public bool ExactMatch { get; set; }
        public bool Interactive { get; set; }
        public ILoggerWithColor Logger { get; set; }
        public string SearchTerm { get; set; }
        public PackageSearchVerbosity Verbosity { get; set; } = PackageSearchVerbosity.Normal;
        public bool JsonFormat { get; set; } = false;

        public PackageSearchArgs(string skip, string take, string format, string verbosity)
        {
            Skip = VerifyInt(skip, DefaultSkip);
            Take = VerifyInt(take, DefaultTake);
            JsonFormat = VerifyFormat(format);
            Verbosity = VerifyVerbosity(verbosity);
        }

        public PackageSearchArgs() { }

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

        public bool VerifyFormat(string format)
        {
            if (!string.IsNullOrEmpty(format))
            {
                return string.Equals(format, "json", StringComparison.CurrentCultureIgnoreCase);
            }

            return false;
        }

        private PackageSearchVerbosity VerifyVerbosity(string verbosity)
        {
            if (string.IsNullOrEmpty(verbosity) || string.Equals(verbosity, nameof(PackageSearchVerbosity.Detailed), StringComparison.CurrentCultureIgnoreCase))
            {
                return PackageSearchVerbosity.Detailed;
            }

            if (string.Equals(verbosity, nameof(PackageSearchVerbosity.Minimal), StringComparison.CurrentCultureIgnoreCase))
            {
                return PackageSearchVerbosity.Minimal;
            }

            return PackageSearchVerbosity.Normal;
        }
    }
}
