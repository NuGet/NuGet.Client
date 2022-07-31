// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.XPlat.Enums
{
    internal static class EnumExtensions
    {
        public static T GetValueFromName<T>(string description) where T : Enum
        {
            description = description?.ToUpperInvariant();
            foreach (var field in typeof(T).GetFields())
            {
                if (field.Name.ToUpperInvariant() == description)
                    return (T)field.GetValue(null);
            }

            throw new ArgumentException("Enum not found.", nameof(description));
        }
    }
}
