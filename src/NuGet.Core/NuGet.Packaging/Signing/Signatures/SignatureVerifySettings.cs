// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Settings to customize Signature.Verify behavior. 
    /// </summary>
    public class SignatureVerifySettings
    {
        /// <summary>
        /// Treat any issue as fatal.
        /// </summary>
        /// <remarks>If set true, any issue will be an error instead of a warning</remarks>
        public bool TreatIssuesAsErrors { get; }

        /// <summary>
        /// Specifies that a signing certificate's chain that chains to an untrusted root is allowed
        /// </summary>
        public bool AllowUntrustedRoot { get; }

        /// <summary>
        /// Specifies that a signing certificate's chain with unkown revocation is allowed.
        /// Set this to true if offline revocation should be allowed.
        /// </summary>
        public bool AllowUnknownRevocation { get; }

        /// <summary>
        /// Specifies that an error should be logged when the signature is expired.
        /// If set to false, this won't allow expired signatures, only skip the logging of the failure.
        /// </summary>
        public bool LogOnSignatureExpired { get; }

        public SignatureVerifySettings(
            bool treatIssuesAsErrors,
            bool allowUntrustedRoot,
            bool allowUnknownRevocation,
            bool logOnSignatureExpired)
        {
            TreatIssuesAsErrors = treatIssuesAsErrors;
            AllowUntrustedRoot = allowUntrustedRoot;
            AllowUnknownRevocation = allowUnknownRevocation;
            LogOnSignatureExpired = logOnSignatureExpired;
        }

        /// <summary>
        /// Get default settings values for relaxed verification on a signature
        /// </summary>
        public static SignatureVerifySettings GetDefault()
        {
            return new SignatureVerifySettings(
             treatIssuesAsErrors: false,
             allowUntrustedRoot: true,
             allowUnknownRevocation: true,
             logOnSignatureExpired: true);
        }
    }
}
