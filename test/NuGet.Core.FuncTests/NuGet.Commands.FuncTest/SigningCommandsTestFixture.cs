// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Commands.FuncTest
{
    public class SigningCommandsTestFixture : IDisposable
    {
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedRepositoryCertificate;
        private readonly TestDirectory _certDir;

        public SigningCommandsTestFixture()
        {
            _certDir = TestDirectory.Create();
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificate
        {
            get
            {
                if (_trustedTestCert == null)
                {
                    _trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate(_certDir);
                }

                return _trustedTestCert;
            }
        }

        // This certificate is interchangeable with TrustedTestCertificate and exists only
        // to provide certificate independence in author + repository signing scenarios.
        public TrustedTestCert<TestCertificate> TrustedRepositoryCertificate
        {
            get
            {
                if (_trustedRepositoryCertificate == null)
                {
                    _trustedRepositoryCertificate = SigningTestUtility.GenerateTrustedTestCertificate(_certDir);
                }

                return _trustedRepositoryCertificate;
            }
        }

        public void Dispose()
        {
            _trustedTestCert?.Dispose();
            _trustedRepositoryCertificate?.Dispose();
            _certDir.Dispose();
        }
    }
}
