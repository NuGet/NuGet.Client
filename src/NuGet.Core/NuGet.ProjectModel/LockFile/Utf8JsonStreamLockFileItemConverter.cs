// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="Utf8JsonStreamReaderConverter{T}"/> to allow read JSON into <see cref="LockFileItem"/>
    /// </summary>
    internal class Utf8JsonStreamLockFileItemConverter<T> : Utf8JsonStreamReaderConverter<T> where T : LockFileItem
    {
        public override T Read(ref Utf8JsonStreamReader reader)
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
    }
}
