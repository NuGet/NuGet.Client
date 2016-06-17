﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("NuGet")]
[assembly: AssemblyCopyright("Microsoft Corporation. All rights reserved.")]
#if !IS_NET40_CLIENT
[assembly: AssemblyMetadata("Serviceable", "True")]
#endif
[assembly: ComVisible(false)]
[assembly: NeutralResourcesLanguage("en-US")]
