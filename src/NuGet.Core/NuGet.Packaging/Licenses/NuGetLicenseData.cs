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
        };

        public static Dictionary<string, ExceptionData> ExceptionList = new Dictionary<string, ExceptionData>()
        {
            {"389-exception", new ExceptionData(licenseID: "389-exception", referenceNumber: 1, isDeprecatedLicenseId: false) },
            {"Autoconf-exception-2.0", new ExceptionData(licenseID: "Autoconf-exception-2.0", referenceNumber: 2, isDeprecatedLicenseId: false) },
        };
    }
}
