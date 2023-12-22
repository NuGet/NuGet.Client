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
            var lazySplitter = new LazyStringSplit(propertyName, LockFile.DirectorySeparatorChar);
            var targetFramework = lazySplitter.FirstOrDefault();
            var runtetimeIdentifier = lazySplitter.FirstOrDefault();
            var leftover = lazySplitter.FirstOrDefault();
            lockFileTarget.TargetFramework = NuGetFramework.Parse(targetFramework);
            if (!string.IsNullOrEmpty(runtetimeIdentifier) && string.IsNullOrEmpty(leftover))
            {
                lockFileTarget.RuntimeIdentifier = runtetimeIdentifier;
            }

            reader.Read();
            lockFileTarget.Libraries = reader.ReadObjectAsList(Utf8JsonReaderExtensions.LockFileTargetLibraryConverter);

            return lockFileTarget;
        }
    }
}
