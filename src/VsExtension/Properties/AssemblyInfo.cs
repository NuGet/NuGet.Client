// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.VisualStudio.Shell;

[assembly: AssemblyTitle("NuGet.Tools")]
[assembly: AssemblyDescription("Visual Studio Extensibility Package (vsix)")]
[assembly: ProvideBindingRedirection(
    AssemblyName = "Newtonsoft.Json",
    PublicKeyToken = "30ad4fe6b2a6aeed",
    Culture = "neutral",
    OldVersionLowerBound = "0.0.0.0",
    OldVersionUpperBound = "6.0.0.0",
    NewVersion = "6.0.0.0")]
