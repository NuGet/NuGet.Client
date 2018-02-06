// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation.PowerConsole;

namespace NuGetClientTestContracts
{
    [Export(typeof(NuGetApexConsoleTestService))]
    public class NuGetApexConsoleTestService
    {
        
    }
}
