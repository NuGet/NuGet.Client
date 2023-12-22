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
            JsonFormat = IsJsonFormat(format);
            Verbosity = GetVerbosityFromOption(verbosity);
        }

        public PackageSearchArgs() { }

        private int VerifyInt(string number, int defaultValue, string option)
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

        private bool IsJsonFormat(string format)
        {
            if (!string.IsNullOrEmpty(format) && string.Equals(format, "json", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private PackageSearchVerbosity GetVerbosityFromOption(string verbosity)
        {
            if (verbosity != null)
            {
                if (string.Equals(verbosity, nameof(PackageSearchVerbosity.Detailed), StringComparison.CurrentCultureIgnoreCase))
                {
                    return PackageSearchVerbosity.Detailed;
                }
                else if (string.Equals(verbosity, nameof(PackageSearchVerbosity.Minimal), StringComparison.CurrentCultureIgnoreCase))
                {
                    return PackageSearchVerbosity.Minimal;
                }
            }

            return PackageSearchVerbosity.Normal;
        }
    }
}
