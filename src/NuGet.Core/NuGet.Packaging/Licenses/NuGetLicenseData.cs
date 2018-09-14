// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging
{

    internal class LicenseData
    {
        public LicenseData(string licenseID, int referenceNumber, bool isOsiApproved, bool isDeprecatedLicenseId)
        {
            LicenseID = licenseID;
            ReferenceNumber = referenceNumber;
            IsOsiApproved = isOsiApproved;
            IsDeprecatedLicenseId = isDeprecatedLicenseId;
        }

        string LicenseID
        {
            get;
        }

        int ReferenceNumber
        {
            get;
        }

        bool IsOsiApproved
        {
            get;
        }

        bool IsDeprecatedLicenseId
        {
            get;
        }
    }

    internal class ExceptionData
    {
        public ExceptionData(string licenseID, int referenceNumber, bool isDeprecatedLicenseId)
        {
            LicenseExceptionID = licenseID;
            ReferenceNumber = referenceNumber;
            IsDeprecatedLicenseId = isDeprecatedLicenseId;
        }

        string LicenseExceptionID
        {
            get;
        }

        int ReferenceNumber
        {
            get;
        }

        bool IsDeprecatedLicenseId
        {
            get;
        }
    }

    internal class NuGetLicenseData
    {
        public static string LicenseListVersion = "v3.1-67-geb2589b";

        public static Dictionary<string, LicenseData> LicenseList = new Dictionary<string, LicenseData>()
        {
            {"0BSD", new LicenseData(licenseID: "0BSD", referenceNumber: 1, isOsiApproved: false, isDeprecatedLicenseId: false) },
            {"AAL", new LicenseData(licenseID: "AAL", referenceNumber: 2, isOsiApproved: true, isDeprecatedLicenseId: false) },
            {"ADSL", new LicenseData(licenseID: "ADSL", referenceNumber: 3, isOsiApproved: false, isDeprecatedLicenseId: false) },
            {"AFL-1.1", new LicenseData(licenseID: "AFL-1.1", referenceNumber: 4, isOsiApproved: true, isDeprecatedLicenseId: false) },
            {"AFL-1.2", new LicenseData(licenseID: "AFL-1.2", referenceNumber: 5, isOsiApproved: true, isDeprecatedLicenseId: false) },
            {"AFL-2.0", new LicenseData(licenseID: "AFL-2.0", referenceNumber: 6, isOsiApproved: true, isDeprecatedLicenseId: false) },
            {"AFL-2.1", new LicenseData(licenseID: "AFL-2.1", referenceNumber: 7, isOsiApproved: true, isDeprecatedLicenseId: false) },
            {"AFL-3.0", new LicenseData(licenseID: "AFL-3.0", referenceNumber: 8, isOsiApproved: true, isDeprecatedLicenseId: false) },
            {"AGPL-1.0-only", new LicenseData(licenseID: "AGPL-1.0-only", referenceNumber: 9, isOsiApproved: false, isDeprecatedLicenseId: false) },
        };

        public static Dictionary<string, ExceptionData> ExceptionList = new Dictionary<string, ExceptionData>()
        {
            {"389-exception", new ExceptionData(licenseID: "389-exception", referenceNumber: 1, isDeprecatedLicenseId: false) },
            {"Autoconf-exception-2.0", new ExceptionData(licenseID: "Autoconf-exception-2.0", referenceNumber: 2, isDeprecatedLicenseId: false) },
            {"Autoconf-exception-3.0", new ExceptionData(licenseID: "Autoconf-exception-3.0", referenceNumber: 3, isDeprecatedLicenseId: false) },
            {"Bison-exception-2.2", new ExceptionData(licenseID: "Bison-exception-2.2", referenceNumber: 4, isDeprecatedLicenseId: false) },
            {"Bootloader-exception", new ExceptionData(licenseID: "Bootloader-exception", referenceNumber: 5, isDeprecatedLicenseId: false) },
            {"CLISP-exception-2.0", new ExceptionData(licenseID: "CLISP-exception-2.0", referenceNumber: 6, isDeprecatedLicenseId: false) },
            {"Classpath-exception-2.0", new ExceptionData(licenseID: "Classpath-exception-2.0", referenceNumber: 7, isDeprecatedLicenseId: false) },
            {"DigiRule-FOSS-exception", new ExceptionData(licenseID: "DigiRule-FOSS-exception", referenceNumber: 8, isDeprecatedLicenseId: false) },
            {"FLTK-exception", new ExceptionData(licenseID: "FLTK-exception", referenceNumber: 9, isDeprecatedLicenseId: false) },
        };
    }
}
