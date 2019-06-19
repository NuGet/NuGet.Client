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
            // Assuming previous validation ran.
            // It is guaranteed that at this point, you can open the icon file

            var reader = builder?.NuspecReader;

            var path = reader?.GetIcon();

            Stream str = builder.GetStream(path);

            if (str.Length > MaxIconFilzeSize)
            {
                yield return PackagingLogMessage.CreateWarning(
                    string.Format(CultureInfo.CurrentCulture, MessageFormat, path),
                    NuGetLogCode.NU5037);
            }
        }
    }
}
