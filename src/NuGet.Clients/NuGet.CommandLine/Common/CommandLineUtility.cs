// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.CommandLine
{
    public static class CommandLineUtility
    {
        public static void ValidateSource(string source)
        {
            Uri result;
            if (!Uri.TryCreate(source, UriKind.Absolute, out result))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("InvalidSource"), source);
            }
        }

        public static string GetSourceDisplayName(string source)
        {
            if (String.IsNullOrEmpty(source) || source.Equals(NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedResourceManager.GetString("LiveFeed") + " (" + NuGetConstants.DefaultGalleryServerUrl + ")";
            }
            if (source.Equals(NuGetConstants.DefaultSymbolServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedResourceManager.GetString("DefaultSymbolServer") + " (" + NuGetConstants.DefaultSymbolServerUrl + ")";
            }
            return "'" + source + "'";
        }

        public static bool IsValidConfigFileName(string fileName)
        {
            return fileName != null &&
                fileName.StartsWith("packages.", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase);
        }
    }
}