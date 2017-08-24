// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation.PowerConsole;

namespace NuGet.Console.TestContract
{
    [Export(typeof(NuGetApexConsoleTestService))]
    public class NuGetApexConsoleTestService
    {
        private IWpfConsole _wpfConsole;

        public NuGetApexConsoleTestService()
        {
            var outputConsoleWindow = ServiceLocator.GetInstance<IPowerConsoleWindow>() as PowerConsoleWindow;
            _wpfConsole = outputConsoleWindow.ActiveHostInfo.WpfConsole;
        }

        public ApexTestConsole GetApexTestConsole()
        {
            return new ApexTestConsole(_wpfConsole);
        }
    }
}
