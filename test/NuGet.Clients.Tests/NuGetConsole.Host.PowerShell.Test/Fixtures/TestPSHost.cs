// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading;

namespace NuGetConsole.Host.PowerShell.Test
{
    /// <summary>
    /// Minimal PSHost that provides PrivateData with properties expected by NuGet cmdlets.
    /// </summary>
    internal sealed class TestPSHost : PSHost
    {
        private readonly Guid _instanceId = Guid.NewGuid();
        private readonly PSObject _privateData;

        public TestPSHost(string activeSource)
        {
            _privateData = new PSObject();
            _privateData.Properties.Add(new PSNoteProperty("activePackageSource", activeSource));
            _privateData.Properties.Add(new PSNoteProperty("CancellationTokenKey", CancellationToken.None));
        }

        public override CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.InvariantCulture;
        public override Guid InstanceId => _instanceId;
        public override string Name => "TestNuGetHost";
        public override PSObject PrivateData => _privateData;
        public override PSHostUserInterface UI => null;
        public override Version Version => new Version(1, 0);

        public override void EnterNestedPrompt() { }
        public override void ExitNestedPrompt() { }
        public override void NotifyBeginApplication() { }
        public override void NotifyEndApplication() { }
        public override void SetShouldExit(int exitCode) { }
    }
}
