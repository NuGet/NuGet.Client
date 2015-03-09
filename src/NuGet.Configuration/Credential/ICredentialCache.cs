#if !DNXCORE50
using System;
using System.Net;

namespace NuGet.Configuration
{
    public interface ICredentialCache
    {
        void Add(Uri uri, ICredentials credentials);
        ICredentials GetCredentials(Uri uri);
    }
}
#endif