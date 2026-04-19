// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// AOT compatibility smoke test entry point.
// All NuGet assemblies with IsAotCompatible=true are referenced via ProjectReference
// and rooted via TrimmerRootAssembly so the ILC compiler analyzes their full public
// surface. A successful publish with IlcTreatWarningsAsErrors=true confirms AOT
// compatibility. No application logic is needed here.
