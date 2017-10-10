// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget sign command
    /// </summary>
    public class SignCommandRunner : ISignCommandRunner
    {
        public int ExecuteCommand(SignArgs signArgs)
        {
            Debugger.Launch();

            // check if the package exists
            if (!File.Exists(signArgs.PackagePath))
            {
                // error to user
                return 1;
            }

            // check if the output directory exists if passed
            if (!string.IsNullOrEmpty(signArgs.OutputDirectory) &&
                !Directory.Exists(signArgs.OutputDirectory))
            {
                //error to user
                return 1;
            }

            var certFindOptions = new CertificateSourceOptions()
            {
                CertificatePath = signArgs.CertificatePath,
                CertificatePassword = signArgs.CertificatePassword,
                Fingerprint = signArgs.CertificateFingerprint,
                StoreLocation = "CurrentUser",
                StoreName = "My",
                SubjectName = signArgs.CertificateSubjectName
            };

            // get matching certificates
            var matchingCerts = CertificateProvider.GetCertificates(certFindOptions);

            if (matchingCerts.Count == 0)
            {
                // error out
                return 1;
            }
            else if(matchingCerts.Count > 1)
            {
                // if on non-windows os or in non interactive mode - display and error out
                return 1;

                // if on windows - launch UI to select
            }
            else
            {
                // invoke lower signing API
                var cert = matchingCerts[0];

                signArgs.Logger.LogInformation($"Signing package with certificate: {cert.Subject}");

                signArgs.Logger.LogInformation($"Signed package with certificate: {cert.Subject}");

                return 0;
            }

        }
    }
}
