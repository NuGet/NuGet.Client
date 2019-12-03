// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Execution;

namespace NuGet.Build.Tasks.Console
{
    /// <summary>
    /// Represents an <see cref="MSBuildItemBase" /> implementation which wraps a <see cref="ProjectInstance" /> object.
    /// </summary>
    internal sealed class MSBuildProjectInstance : MSBuildItemBase
    {
        /// <summary>
        /// Gets the <see cref="ProjectInstance" /> being wrapped.
        /// </summary>
        private readonly ProjectInstance _projectInstance;

        /// <summary>
        /// Initializes a new instance of the <see cref="MSBuildProjectInstance" /> class.
        /// </summary>
        /// <param name="projectInstance">The <see cref="ProjectInstance" /> to wrap.</param>
        public MSBuildProjectInstance(ProjectInstance projectInstance)
        {
            _projectInstance = projectInstance ?? throw new ArgumentNullException(nameof(projectInstance));
        }

        /// <inheritdoc cref="MSBuildItemBase.GetPropertyValue(string)" />
        protected override string GetPropertyValue(string name) => _projectInstance.GetPropertyValue(name);
    }
}
