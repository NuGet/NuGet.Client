// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Represents an API providing read-only access to 
    /// project's build properties.
    /// </summary>
    public interface IProjectBuildProperties
    {
        /// <summary>
        /// Returns a property value.
        /// </summary>
        /// <param name="propertyName">A property name</param>
        /// <returns>Property value or <code>null</code> if not found.</returns>
        string GetPropertyValue(string propertyName);

        /// <summary>
        /// Asynchronous method to retrieve a property value.
        /// </summary>
        /// <param name="propertyName">A property name</param>
        /// <returns>Property value or <code>null</code> if not found.</returns>
        Task<string> GetPropertyValueAsync(string propertyName);
    }
}
