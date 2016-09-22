// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("NuGet.VisualStudio.SolutionRestoreManager")]
[assembly: AssemblyDescription("APIs for invoking NuGet services in Visual Studio.")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// We're not really importing anything from a type library. This is just to make VS happy so we can embed interop types when 
// referencing this assembly
[assembly: ImportedFromTypeLib("NuGet.VisualStudio.SolutionRestoreManager")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("4003e1ab-70de-4b9c-8999-96160ee91d84")]
