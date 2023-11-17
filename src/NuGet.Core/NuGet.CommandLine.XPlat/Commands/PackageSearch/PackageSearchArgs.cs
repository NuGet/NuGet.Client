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
            Skip = VerifyInt(skip, DefaultSkip, "--skip");
            Take = VerifyInt(take, DefaultTake, "--take");
            JsonFormat = VerifyFormat(format);
            Verbosity = VerifyVerbosity(verbosity);
        }

        public PackageSearchArgs() { }

        public int VerifyInt(string number, int defaultValue, string option)
        {
            if (string.IsNullOrEmpty(number))
            {
                return defaultValue;
            }

            if (int.TryParse(number, out int verifiedNumber))
            {
                return verifiedNumber;
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_invalidOptionValue, number, option));
        }

        public bool VerifyFormat(string format)
        {
            if (!string.IsNullOrEmpty(format))
            {
                if (string.Equals(format, "json", StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
                else if (string.Equals(format, "table", StringComparison.CurrentCultureIgnoreCase))
                {
                    return false;
                }
                else
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_invalidOptionValue, format, "--format"));
                }
            }

            return false;
        }

        private PackageSearchVerbosity VerifyVerbosity(string verbosity)
        {
            if (verbosity != null)
            {
                if (string.Equals(verbosity, nameof(PackageSearchVerbosity.Detailed), StringComparison.CurrentCultureIgnoreCase))
                {
                    return PackageSearchVerbosity.Detailed;
                }
                else if (string.Equals(verbosity, nameof(PackageSearchVerbosity.Normal), StringComparison.CurrentCultureIgnoreCase))
                {
                    return PackageSearchVerbosity.Normal;
                }
                else if (string.Equals(verbosity, nameof(PackageSearchVerbosity.Minimal), StringComparison.CurrentCultureIgnoreCase))
                {
                    return PackageSearchVerbosity.Minimal;
                }
                else
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_invalidOptionValue, verbosity, "--verbosity"));
                }
            }

            return PackageSearchVerbosity.Normal;
        }
    }
}
