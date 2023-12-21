// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using NuGet.Frameworks;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="Utf8JsonStreamReaderConverter{T}"/> to allow read JSON into <see cref="LockFileTarget"/>
    /// </summary>
    internal class Utf8JsonStreamLockFileTargetConverter : IUtf8JsonStreamReaderConverter<LockFileTarget>
    {
        public LockFileTarget Read(ref Utf8JsonStreamReader reader)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var lockFileTarget = new LockFileTarget();
            //We want to read the property name right away
            var propertyName = reader.GetString();
            var parts = GetFrameworkAndIdentifier(propertyName, LockFile.DirectorySeparatorChar);
            lockFileTarget.TargetFramework = NuGetFramework.Parse(parts.targetFramework);
            lockFileTarget.RuntimeIdentifier = parts.runtimeIdentifier;

            reader.Read();
            lockFileTarget.Libraries = reader.ReadObjectAsList(Utf8JsonReaderExtensions.LockFileTargetLibraryConverter);

            return lockFileTarget;
        }


        public static (string targetFramework, string runtimeIdentifier) GetFrameworkAndIdentifier(string input, char separator)
        {
            int firstIndex = input.IndexOf(separator);
            int lastIndex = input.LastIndexOf(separator);

            if (firstIndex == -1)
                return (input, null);

            return (input.Substring(0, firstIndex),
                    firstIndex >= input.Length - 1 || firstIndex != lastIndex ? null : input.Substring(firstIndex + 1));
        }
    }
}
