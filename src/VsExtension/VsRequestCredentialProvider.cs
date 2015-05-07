// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGetVSExtension
{
    public class VSRequestCredentialProvider : VisualStudioCredentialProvider
    {
        public VSRequestCredentialProvider(IVsWebProxy webProxy)
            : base(webProxy)
        {
        }

        protected override void InitializeCredentialProxy(Uri uri, IWebProxy originalProxy)
        {
            WebRequest.DefaultWebProxy = new WebProxy(uri);
        }
    }
}
