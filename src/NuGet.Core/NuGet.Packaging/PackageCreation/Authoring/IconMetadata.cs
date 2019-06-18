// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class IconMetadata : IEquatable<IconMetadata>
    {
        public IconType Type { get; }

        public string Path { get; }

        public IconMetadata(IconType iconType, string path)
        {
            Type = iconType;
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public bool Equals(IconMetadata other)
        {
            throw new NotImplementedException();
        }
    }

    public enum IconType
    {
        File
    }
}
