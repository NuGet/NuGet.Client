// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio;

namespace NuGet.Test.TestExtensions.TestableVSCredentialProvider
{

    [Export(typeof(IVsCredentialProvider))]
    public sealed class TestCredentialProvider2
        : TestCredentialProvider
    {
    }
}
