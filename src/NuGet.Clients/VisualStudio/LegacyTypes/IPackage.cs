﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Versioning;

namespace NuGet
{
    public interface IPackage
    {
        bool IsAbsoluteLatestVersion { get; }

        bool IsLatestVersion { get; }

        bool Listed { get; }

        DateTimeOffset? Published { get; }

        IEnumerable<IPackageAssemblyReference> AssemblyReferences { get; }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This might be expensive")]
        IEnumerable<IPackageFile> GetFiles();

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This might be expensive")]
        IEnumerable<FrameworkName> GetSupportedFrameworks();

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This might be expensive")]
        Stream GetStream();

        void ExtractContents(IFileSystem fileSystem, string extractPath);
    }
}
