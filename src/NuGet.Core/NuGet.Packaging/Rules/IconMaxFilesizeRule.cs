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

        private const int BufferSize = 1024 * 1024;

        public string MessageFormat { get; }

        public IconMaxFilesizeRule(string messageFormat)
        {
            MessageFormat = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var reader = builder?.NuspecReader;

            var path = reader?.GetIcon();

            var issues = new List<PackagingLogMessage>();

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    using (var str = builder.GetStream(path))
                    {
                        long fileSize = EstimateFileSize(str);

                        if (fileSize > MaxIconFilzeSize || (fileSize == MaxIconFilzeSize && str.CanRead))
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
        /// <param name="str"></param>
        /// <returns></returns>
        private static long EstimateFileSize(Stream str)
        {
            long fileSize = -1;
            try
            {
                fileSize = str.Length;
            }
            catch(NotSupportedException)
            {
                byte[] byteBuffer = new byte[BufferSize];

                fileSize = 0;
                while(str.CanRead && fileSize < MaxIconFilzeSize)
                {
                    fileSize += str.Read(byteBuffer, 0, BufferSize);
                }
            }

            return fileSize;
        }
    }
}
