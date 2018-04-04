// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public class SignatureVerifySettings
    {
        public bool TreatIssuesAsErrors { get; }

        public bool AllowUntrustedRoot { get; }

        public bool AllowUnknownRevocation { get; }

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

        public static SignatureVerifySettings Default { get; } = new SignatureVerifySettings(
            treatIssuesAsErrors: false,
            allowUntrustedRoot: true,
            allowUnknownRevocation: true,
            logOnSignatureExpired: true);

    }
}
