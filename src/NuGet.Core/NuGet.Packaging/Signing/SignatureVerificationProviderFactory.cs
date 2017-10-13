using System.Collections.Generic;
using NuGet.Packaging.Test.SigningTests;

namespace NuGet.Packaging.Signing
{
    public static class SignatureVerificationProviderFactory
    {
        public static IEnumerable<ISignatureVerificationProvider> GetSignatureVerificationProviders()
        {
            yield return new SignatureVerificationProvider();
        }
    }
}
