// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="Utf8JsonStreamReaderConverter{T}"/> to allow read JSON into <see cref="ProjectFileDependencyGroup"/>
    /// </summary>
    internal class Utf8JsonStreamProjectFileDependencyGroupConverter : IUtf8JsonStreamReaderConverter<ProjectFileDependencyGroup>
    {
        public ProjectFileDependencyGroup Read(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var frameworkName = reader.GetString();
            reader.Read();
            var dependencies = reader.ReadStringArrayAsIList(new List<string>());

            return new ProjectFileDependencyGroup(frameworkName, dependencies);
        }
    }
}
