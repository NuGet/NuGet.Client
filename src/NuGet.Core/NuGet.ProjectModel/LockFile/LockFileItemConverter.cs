// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="LockFileItem"/>
    /// </summary>
    internal class LockFileItemConverter<T> : StreamableJsonConverter<T> where T : LockFileItem
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var genericType = typeof(T);
            if (typeToConvert != genericType)
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            //We want to read the property name right away
            var lockItemPath = reader.GetString();
            LockFileItem lockFileItem;
            if (genericType == typeof(LockFileContentFile))
            {
                lockFileItem = new LockFileContentFile(lockItemPath);
            }
            else if (genericType == typeof(LockFileRuntimeTarget))
            {
                lockFileItem = new LockFileRuntimeTarget(lockItemPath);
            }
            else
            {
                lockFileItem = new LockFileItem(lockItemPath);
            }

            reader.ReadNextToken();
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    lockFileItem.Properties[propertyName] = reader.ReadNextTokenAsString();
                }
            }

            return lockFileItem as T;
        }

        public override T ReadWithStream(ref Utf8JsonStreamReader reader, JsonSerializerOptions options)
        {
            var genericType = typeof(T);

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            //We want to read the property name right away
            var lockItemPath = reader.GetString();
            LockFileItem lockFileItem;
            if (genericType == typeof(LockFileContentFile))
            {
                lockFileItem = new LockFileContentFile(lockItemPath);
            }
            else if (genericType == typeof(LockFileRuntimeTarget))
            {
                lockFileItem = new LockFileRuntimeTarget(lockItemPath);
            }
            else
            {
                lockFileItem = new LockFileItem(lockItemPath);
            }

            reader.Read();
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    lockFileItem.Properties[propertyName] = reader.ReadNextTokenAsString();
                }
            }

            return lockFileItem as T;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
