// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Collection of constants representing project item properties names.
    /// </summary>
    public static class ProjectItemProperties
    {
        public const string IncludeAssets = "IncludeAssets";
        public const string ExcludeAssets = "ExcludeAssets";
        public const string PrivateAssets = "PrivateAssets";
        public const string IsImplicitlyDefined = nameof(IsImplicitlyDefined);
        public const string NoWarn = nameof(NoWarn);
        public const string GeneratePathProperty = "GeneratePathProperty";
    }
}
