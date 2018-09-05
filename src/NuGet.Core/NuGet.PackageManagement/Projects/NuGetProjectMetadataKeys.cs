// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.ProjectManagement
{
    public static class NuGetProjectMetadataKeys
    {
        // The name of the project, e.g. "ConsoleApplication1"
        public const string Name = "Name";

        // The name of the project, relative to the solution. e.g. "src\ConsoleApplication1"
        public const string UniqueName = "UniqueName";

        public const string TargetFramework = "TargetFramework";
        public const string FullPath = "FullPath";

        // used by Project K projects
        public const string SupportedFrameworks = "SupportedFrameworks";

        public const string ProjectId = "ProjectId";
    }
}
