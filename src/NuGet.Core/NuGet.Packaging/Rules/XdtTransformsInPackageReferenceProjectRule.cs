// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class XdtTransformInPackageReferenceProjectRule : IPackageRule
    {
        private const string ConfigTransformExtension = ".transform";
        private const string ContentDirectory = "content";
        private const string ContentFilesDirectory = "contentFiles";

        public string MessageFormat { get; }

        public XdtTransformInPackageReferenceProjectRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            foreach (var file in builder.GetFiles().Select(t => PathUtility.GetPathWithDirectorySeparator(t)))
            {
                // if not a .transform file, ignore
                if (!file.EndsWith(ConfigTransformExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // if inside content or contentFiles folder then warn.
                if (file.StartsWith(ContentDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    || file.StartsWith(ContentFilesDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreatePackageIssueForTransformFiles(file);
                }
            }
        }

        private PackagingLogMessage CreatePackageIssueForTransformFiles(string path)
        {
            return PackagingLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, MessageFormat, path),
                NuGetLogCode.NU5122);
        }
    }
}