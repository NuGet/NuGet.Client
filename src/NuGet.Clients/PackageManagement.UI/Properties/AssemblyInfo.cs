// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Markup;


#if SIGNED_BUILD
[assembly: InternalsVisibleTo("NuGet.PackageManagement.UI.Test, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]
[assembly: InternalsVisibleTo("NuGet.PackageManagement.PowerShellCmdlets, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]
#else
[assembly: InternalsVisibleTo("NuGet.PackageManagement.UI.Test")]
[assembly: InternalsVisibleTo("NuGet.PackageManagement.PowerShellCmdlets")]
#endif

[assembly: AssemblyTitle("NuGet's Package Management UI for Visual Studio")]
[assembly: AssemblyDescription("NuGet's Package Management UI for Visual Studio")]
[assembly: ComVisible(false)]

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: XmlnsPrefix("http://schemas.nuget.org/xaml", "nuget")]
[assembly: XmlnsDefinition("http://schemas.nuget.org/xaml", "NuGet.PackageManagement.UI")]