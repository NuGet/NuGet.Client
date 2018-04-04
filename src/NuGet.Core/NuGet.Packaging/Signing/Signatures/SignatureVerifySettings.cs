// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    public class SignatureVerifySettings
    {
        public bool TreatIssueAsError { get; }

        public bool AllowUntrustedRoot { get; }

        public bool AllowUnknownRevocation { get; }

        public SignatureVerifySettings(
            bool treatIssueAsError,
            bool allowUntrustedRoot,
            bool allowUnknownRevocation)
        {
            TreatIssueAsError = treatIssueAsError;
            AllowUntrustedRoot = allowUntrustedRoot;
            AllowUnknownRevocation = allowUnknownRevocation;
        }

        public static SignatureVerifySettings Default { get; } = new SignatureVerifySettings(
            treatIssueAsError: false,
            allowUntrustedRoot: true,
            allowUnknownRevocation: true);

    }
}
