// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Frameworks;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="LockFileTarget"/>
    /// </summary>
    internal class LockFileTargetConverter : StreamableJsonConverter<LockFileTarget>
    {
        public override LockFileTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(LockFileTarget))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var lockFileTarget = new LockFileTarget();
            //We want to read the property name right away
            var propertyName = reader.GetString();
            var parts = propertyName.Split(JsonUtility.PathSplitChars, 2);
            lockFileTarget.TargetFramework = NuGetFramework.Parse(parts[0]);
            if (parts.Length == 2)
            {
                lockFileTarget.RuntimeIdentifier = parts[1];
            }

            reader.ReadNextToken();
            lockFileTarget.Libraries = reader.ReadObjectAsList<LockFileTargetLibrary>(options);

            return lockFileTarget;
        }

        public override LockFileTarget ReadWithStream(ref StreamingUtf8JsonReader reader, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var lockFileTarget = new LockFileTarget();
            //We want to read the property name right away
            var propertyName = reader.GetString();
            var parts = propertyName.Split(JsonUtility.PathSplitChars, 2);
            lockFileTarget.TargetFramework = NuGetFramework.Parse(parts[0]);
            if (parts.Length == 2)
            {
                lockFileTarget.RuntimeIdentifier = parts[1];
            }

            reader.Read();
            lockFileTarget.Libraries = reader.ReadObjectAsList<LockFileTargetLibrary>(options);

            return lockFileTarget;
        }

        public override void Write(Utf8JsonWriter writer, LockFileTarget value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
