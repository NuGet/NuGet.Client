// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging
{
    internal class NuGetLicenseData
    {
        public static string LicenseListVersion = "listversion";

        public static Dictionary<string, LicenseData> LicenseList = new Dictionary<string, LicenseData>()
        {
            {"licenseID", new LicenseData("licenseID", 0, true, true) },
        };

        public static Dictionary<string, ExceptionData> ExceptionList = new Dictionary<string, ExceptionData>()
        {
            {"exceptionID", new ExceptionData("exceptionID", 0, true) },
        };
    }

    internal class LicenseData
    {
        public LicenseData(string licenseID, int referenceNumber, bool isOsiApproved, bool isDeprecatedLicenseId)
        {
            LicenseID = licenseID;
            ReferenceNumber = referenceNumber;
            IsOsiApproved = isOsiApproved;
            IsDeprecatedLicenseId = isDeprecatedLicenseId;
        }

        string LicenseID { get; }
        int ReferenceNumber { get; }
        bool IsOsiApproved { get; }
        bool IsDeprecatedLicenseId { get; }
    }

    internal class ExceptionData
    {
        public ExceptionData(string licenseID, int referenceNumber, bool isDeprecatedLicenseId)
        {
            LicenseExceptionID = licenseID;
            ReferenceNumber = referenceNumber;
            IsDeprecatedLicenseId = isDeprecatedLicenseId;
        }

        string LicenseExceptionID { get; }
        int ReferenceNumber { get; }
        bool IsDeprecatedLicenseId { get; }
    }
}
