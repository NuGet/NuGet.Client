// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Test.Utility.Signing;

namespace NuGet.Packaging.FuncTest
{
    /// <summary>
    /// Used to bootstrap functional tests for signing.
    /// </summary>
    public class SigningTestFixture : IDisposable
    {
        private static readonly string _testTimestampServer = Environment.GetEnvironmentVariable("TIMESTAMP_SERVER_URL");

        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedTestCertExpired;
        private TrustedTestCert<TestCertificate> _trustedTestCertNotYetValid;
        private IReadOnlyList<TrustedTestCert<TestCertificate>> _trustedTestCertificateWithReissuedCertificate;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;

        public TrustedTestCert<TestCertificate> TrustedTestCertificate
        {
            get
            {
                if (_trustedTestCert == null)
                {
                    var actionGenerator = SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert;

                    // Code Sign EKU needs trust to a root authority
                    // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
                    // This makes all the associated tests to require admin privilege
                    _trustedTestCert = TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine);
                }

                return _trustedTestCert;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateExpired
        {
            get
            {
                if (_trustedTestCertExpired == null)
                {
                    var actionGenerator = SigningTestUtility.CertificateModificationGeneratorExpiredCert;

                    // Code Sign EKU needs trust to a root authority
                    // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
                    // This makes all the associated tests to require admin privilege
                    _trustedTestCertExpired = TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine);
                }

                return _trustedTestCertExpired;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateNotYetValid
        {
            get
            {
                if (_trustedTestCertNotYetValid == null)
                {
                    var actionGenerator = SigningTestUtility.CertificateModificationGeneratorNotYetValidCert;

                    // Code Sign EKU needs trust to a root authority
                    // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
                    // This makes all the associated tests to require admin privilege
                    _trustedTestCertNotYetValid = TestCertificate.Generate(actionGenerator).WithTrust(StoreName.Root, StoreLocation.LocalMachine);
                }

                return _trustedTestCertNotYetValid;
            }
        }

        public IReadOnlyList<TrustedTestCert<TestCertificate>> TrustedTestCertificateWithReissuedCertificate
        {
            get
            {
                if (_trustedTestCertificateWithReissuedCertificate == null)
                {
                    var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
                    var certificateName = TestCertificate.GenerateCertificateName();
                    var certificate1 = SigningTestUtility.GenerateCertificate(certificateName, keyPair);
                    var certificate2 = SigningTestUtility.GenerateCertificate(certificateName, keyPair);

                    var testCertificate1 = new TestCertificate() { Cert = certificate1 }.WithTrust(StoreName.Root, StoreLocation.LocalMachine);
                    var testCertificate2 = new TestCertificate() { Cert = certificate2 }.WithTrust(StoreName.Root, StoreLocation.LocalMachine);

                    _trustedTestCertificateWithReissuedCertificate = new[]
                    {
                        testCertificate1,
                        testCertificate2
                    };
                }

                return _trustedTestCertificateWithReissuedCertificate;
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

        public string Timestamper => _testTimestampServer;

        public void Dispose()
        {
            _trustedTestCert?.Dispose();
        }
    }
}