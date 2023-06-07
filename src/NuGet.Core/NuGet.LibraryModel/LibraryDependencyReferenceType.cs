// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.LibraryModel
{
    public enum LibraryDependencyReferenceType
    {
        // This enum is carefully packed in LibraryDependency.
        // Any changes here should be verified there too.

        None,
        Transitive,
        Direct
    }
}
