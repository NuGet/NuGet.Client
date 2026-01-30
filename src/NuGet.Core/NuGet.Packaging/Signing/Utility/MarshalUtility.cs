// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace NuGet.Packaging.Signing.Utility
{
    internal static class MarshalUtility
    {
        internal static T PtrToStructure<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(IntPtr pointer)
        {
            return Marshal.PtrToStructure<T>(pointer);
        }

        internal static int SizeOf<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] T>()
        {
            return Marshal.SizeOf<T>();
        }
    }
}
