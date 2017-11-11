// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NuGet.Packaging.FuncTest")]
namespace NuGet.Packaging.Signing
{
    internal static class Oids
    {
        // RFC 5652 "signing-time" attribute https://tools.ietf.org/html/rfc5652#section-11.3
        internal const string SigningTimeOid = "1.2.840.113549.1.9.5";

        // RFC 3161 appendix A (https://tools.ietf.org/html/rfc3161#page-20)
        internal const string SignatureTimeStampTokenAttributeOid = "1.2.840.113549.1.9.16.2.14";

        // RFC 8017 appendix B.1 (https://tools.ietf.org/html/rfc8017#appendix-B.1).
        internal const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

        // RFC 8017 appendix B.1 (https://tools.ietf.org/html/rfc8017#appendix-B.1).
        internal const string Sha512Oid = "2.16.840.1.101.3.4.2.3";

        // RFC 5280 codeSigning attribute, https://tools.ietf.org/html/rfc5280#section-4.2.1.12
        internal const string CodeSigningEkuOid = "1.3.6.1.5.5.7.3.3";

        // RFC 5652 "id-data" https://tools.ietf.org/html/rfc5652#section-4
        internal const string Pkcs7DataOid = "1.2.840.113549.1.7.1";

        // ETSI TS 102 023 v1.2.2 http://www.etsi.org/deliver/etsi_ts/102000_102099/102023/01.02.02_60/ts_102023v010202p.pdf
        internal const string BaselineTimestampPolicyOid = "0.4.0.2023.1.1";

        // RFC 3280 "id-kp-timeStamping" https://tools.ietf.org/html/rfc3280.html#section-4.2.1.13
        internal const string TimeStampingEkuOid = "1.3.6.1.5.5.7.3.8";

        // RFC 2459 "id-ce-extKeyUsage" https://tools.ietf.org/html/rfc3280.html#section-4.2.1.13
        internal const string EnhancedKeyUsageOid = "2.5.29.37";
    }
}
