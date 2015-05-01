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
