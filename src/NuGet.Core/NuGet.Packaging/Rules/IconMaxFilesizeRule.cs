// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NuGet.Common;
using System.CodeDom;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NuGet.Packaging.Rules
{
    public class IconMaxFilesizeRule : IPackageRule
    {
        private const int MaxIconFilzeSize = 1024 * 1024;

        public string MessageFormat { get; }

        public IconMaxFilesizeRule(string messageFormat)
        {
            MessageFormat = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            NuspecReader reader = builder?.NuspecReader;

            string path = reader?.GetIcon();

            List<PackagingLogMessage> issues = new List<PackagingLogMessage>();

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    using (var str = builder.GetStream(path))
                    {
                        long fileSize = EstimateFileSize(str);

                        if (fileSize > MaxIconFilzeSize)
                        {
                            issues.Add(PackagingLogMessage.CreateWarning(
                                string.Format(CultureInfo.CurrentCulture, MessageFormat, path, AnalysisResources.IconMaxFilsesizeExceeded),
                                NuGetLogCode.NU5037));
                        }
                    }
                }
                catch (FileNotFoundException e)
                {
                    issues.Add(PackagingLogMessage.CreateWarning(
                                string.Format(CultureInfo.CurrentCulture, MessageFormat, path, e.Message),
                                NuGetLogCode.NU5036));
                }
            }

            return issues;
        }

        /// <summary>
        /// Reads up to MaxIconFilzeSize of the file
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private static long EstimateFileSize(Stream stream)
        {
            long fileSize = -1;
            try
            {
                fileSize = stream.Length;
            }
            catch(NotSupportedException)
            {
                int buffSizeee = MaxIconFilzeSize + 1;
                byte[] byteBuffer = new byte[buffSizeee];

                if (stream.CanRead)
                {
                    fileSize = stream.Read(byteBuffer, 0, buffSizeee);
                }
            }

            return fileSize;
        }
    }
}
