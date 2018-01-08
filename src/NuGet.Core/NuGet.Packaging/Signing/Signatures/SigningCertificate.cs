// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Signing.DerEncoding;

namespace NuGet.Packaging.Signing
{
    /*
        From RFC 2634 (https://tools.ietf.org/html/rfc2634#section-5.4):

            SigningCertificate ::= SEQUENCE {
                certs        SEQUENCE OF ESSCertID,
                policies     SEQUENCE OF PolicyInformation OPTIONAL
            }
    */
    /// <remarks>This is public only to facilitate testing.</remarks>
    public sealed class SigningCertificate
    {
        public IReadOnlyList<EssCertId> Certificates { get; }

        private SigningCertificate(IReadOnlyList<EssCertId> certificates)
        {
            Certificates = certificates;
        }

        public static SigningCertificate Read(byte[] bytes)
        {
            var reader = new DerSequenceReader(bytes);

            return Read(reader);
        }

        internal static SigningCertificate Read(DerSequenceReader reader)
        {
            var essCertIdReader = reader.ReadSequence();
            var certificates = ReadCertificates(essCertIdReader);

            // Skip the "policies" field.  We do not use it.

            return new SigningCertificate(certificates.AsReadOnly());
        }

        private static List<EssCertId> ReadCertificates(DerSequenceReader reader)
        {
            var certificates = new List<EssCertId>();

            while (reader.HasData)
            {
                var certificate = EssCertId.Read(reader);

                certificates.Add(certificate);
            }

            return certificates;
        }
    }
}
