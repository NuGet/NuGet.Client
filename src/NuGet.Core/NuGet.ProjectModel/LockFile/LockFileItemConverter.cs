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
    internal class LockFileItemConverter<T> : JsonConverter<T> where T : LockFileItem
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(T))
            {
                throw new InvalidOperationException();
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            //We want to read the property name right away
            var lockItemPath = reader.GetString();
            LockFileItem lockFileItem = typeof(T) == typeof(LockFileContentFile) ?
                new LockFileContentFile(lockItemPath) :
                typeof(T) == typeof(LockFileRuntimeTarget) ?
                new LockFileRuntimeTarget(lockItemPath) :
                new LockFileItem(lockItemPath);

            reader.Read();
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                reader.Read();
                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propertyName = reader.GetString();
                        reader.Read();
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.String:
                                lockFileItem.Properties[propertyName] = reader.GetString();
                                break;
                            case JsonTokenType.Number:
                                lockFileItem.Properties[propertyName] = reader.GetInt32().ToString();
                                break;
                            case JsonTokenType.True:
                            case JsonTokenType.False:
                                lockFileItem.Properties[propertyName] = reader.GetBoolean().ToString();
                                break;
                            default:
                                throw new JsonException("Expected String, Number, True, or False, found " + reader.TokenType);
                        }
                    }
                    reader.Read();
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
