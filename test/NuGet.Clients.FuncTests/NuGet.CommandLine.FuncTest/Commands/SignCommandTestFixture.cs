// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.CommandLine.Test;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using Test.Utility.Signing;

namespace NuGet.CommandLine.FuncTest.Commands
{
    /// <summary>
    /// Used to bootstrap functional tests for signing.
    /// </summary>
    public class SignCommandTestFixture : IDisposable
    {
        private const string _timestamper = "http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer";

        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedTestCertWithInvalidEku;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;
        private string _nugetExePath;

        public TrustedTestCert<TestCertificate> TrustedTestCertificate
        {
            get
            {
                if (_trustedTestCert == null)
                {
                    var actionGenerator = SigningTestUtility.CertificateModificationGeneratorForCodeSigningEku;

                    // Code Sign EKU needs trust to a root authority
                    // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
                    // This makes all the associated tests to require admin privilege
                    _trustedTestCert = TestCertificate.Generate(actionGenerator).WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine);
                }

                return _trustedTestCert;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateWithInvalidEku
        {
            get
            {
                if (_trustedTestCertWithInvalidEku == null)
                {
                    var actionGenerator = SigningTestUtility.CertificateModificationGeneratorForInvalidEku;

                    // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
                    // This makes all the associated tests to require admin privilege
                    _trustedTestCertWithInvalidEku = TestCertificate.Generate(actionGenerator).WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine);
                }

                return _trustedTestCertWithInvalidEku;
            }
        }

        public IList<ISignatureVerificationProvider> TrustProviders
        {
            get
            {
                if (_trustProviders == null)
                {
                    _trustProviders = new List<ISignatureVerificationProvider>()
                    {
                        new SignatureTrustAndValidityVerificationProvider(),
                        new IntegrityVerificationProvider()
                    };
                }

                return _trustProviders;
            }
        }

        public SigningSpecifications SigningSpecifications
        {
            get
            {
                if (_signingSpecifications == null)
                {
                    _signingSpecifications = SigningSpecifications.V1;
                }

                return _signingSpecifications;
            }
        }

        public string NuGetExePath
        {
            get
            {
                if (_nugetExePath == null)
                {
                    _nugetExePath = Util.GetNuGetExePath();
                }

                return _nugetExePath;
            }
        }

        public string Timestamper => _timestamper;

        public void Dispose()
        {
            _trustedTestCert?.Dispose();
            _trustedTestCertWithInvalidEku?.Dispose();
        }
    }
}
