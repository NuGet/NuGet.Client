// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation.PowerConsole;

namespace NuGet.Console.TestContract
{
    [Export(typeof(NuGetApexConsoleTestService))]
    public class NuGetApexConsoleTestService
    {
        private Lazy<IWpfConsole> _wpfConsole => new Lazy<IWpfConsole>(GetWpfConsole);
        private IWpfConsole GetWpfConsole()
        {
            var outputConsoleWindow = ServiceLocator.GetInstance<IPowerConsoleWindow>() as PowerConsoleWindow;
            return outputConsoleWindow.ActiveHostInfo.WpfConsole;
        }

        public NuGetApexConsoleTestService()
        {
        }

        public ApexTestConsole GetApexTestConsole()
        {
            return new ApexTestConsole(_wpfConsole.Value);
        }
    }
}
