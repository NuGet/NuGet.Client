// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;


namespace NuGet.Packaging.Rules
{
    public class MissingReadmeRule : IPackageRule
    {
        public string MessageFormat { get; }

        public MissingReadmeRule(string messageFormat)
        {
            MessageFormat = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var nuspecReader = builder?.NuspecReader;
            var readmeMetadata = nuspecReader?.GetReadme();

            if (readmeMetadata == null)
            {
                yield return PackagingLogMessage.CreateMessage(
                    string.Format(CultureInfo.CurrentCulture, Environment.NewLine + MessageFormat),
                    LogLevel.Minimal);
            }
        }
    }
}
