// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.ProjectModel
{
    public enum ProjectStyle : ushort
    {
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// UAP style, project.lock.json is generated next to project.json
        /// </summary>
        ProjectJson = 1,

        /// <summary>
        /// MSBuild style, project.assets.json is generated in the RestoreOutputPath folder
        /// </summary>
        PackageReference = 2,

        /// <summary>
        /// DotnetToolReference project
        /// </summary>
        DotnetToolReference = 3,

        /// <summary>
        /// DotnetCliToolReference "project"
        /// </summary>
        DotnetCliTool = 4,

        /// <summary>
        /// Non-MSBuild project with no project dependencies.
        /// </summary>
        Standalone = 5,

        /// <summary>
        /// Packages.config project
        /// </summary>
        PackagesConfig = 6
    }
}