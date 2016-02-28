// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Versioning;

namespace NuGet.Packaging
{
    public interface IPackageFile
    {
        /// <summary>
        /// Gets the full path of the file inside the package.
        /// </summary>
        string Path
        {
            get;
        }

        /// <summary>
        /// Gets the path that excludes the root folder (content/lib/tools) and framework folder (if present).
        /// </summary>
        /// <example>
        /// If a package has the Path as 'content\[net40]\scripts\jQuery.js', the EffectivePath 
        /// will be 'scripts\jQuery.js'.
        /// 
        /// If it is 'tools\init.ps1', the EffectivePath will be 'init.ps1'.
        /// </example>
        string EffectivePath
        {
            get;
        }

        FrameworkName TargetFramework
        {
            get;
        }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This might be expensive")]
        Stream GetStream();
    }
}