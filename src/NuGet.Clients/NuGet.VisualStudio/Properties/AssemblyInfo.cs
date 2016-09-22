﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("NuGet.VisualStudio")]
[assembly: AssemblyDescription("APIs for invoking NuGet services in Visual Studio.")]

// We're not really importing anything from a type library. This is just to make VS happy so we can embed interop types when 
// referencing this assembly

[assembly: ImportedFromTypeLib("NuGet.VisualStudio")]
[assembly: Guid("228F7591-2777-47D7-B81D-FEADFC71CEB5")]
[assembly: ComVisible(false)]
