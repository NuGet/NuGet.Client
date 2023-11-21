// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="ProjectFileDependencyGroup"/> where the list is setup as an object
    /// </summary>
    internal class ProjectFileDependencyGroupConverter : StreamableJsonConverter<ProjectFileDependencyGroup>
    {
        public override ProjectFileDependencyGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(ProjectFileDependencyGroup))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var stringListDefaultConverter = (JsonConverter<IList<string>>)options.GetConverter(typeof(IList<string>));

            var frameworkName = reader.GetString();
            reader.ReadNextToken();
            var dependencies = stringListDefaultConverter.Read(ref reader, typeof(IList<string>), options);

            return new ProjectFileDependencyGroup(frameworkName, dependencies);
        }

        public override ProjectFileDependencyGroup ReadWithStream(ref Utf8JsonStreamReader reader, JsonSerializerOptions options)
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

        public override void Write(Utf8JsonWriter writer, ProjectFileDependencyGroup value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
