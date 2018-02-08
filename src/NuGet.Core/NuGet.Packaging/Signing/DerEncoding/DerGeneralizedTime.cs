// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;

namespace NuGet.Packaging.Signing.DerEncoding
{
    /// <remarks>This is public only to facilitate testing.</remarks>
    public sealed class DerGeneralizedTime
    {
        public DateTime DateTime { get; }

        private DerGeneralizedTime(DateTime dateTime)
        {
            DateTime = dateTime;
        }

        public static DerGeneralizedTime Read(string decodedTime)
        {
            // YYYYMMDDhhmmssZ
            var minimumValidLength = 15;

            if (string.IsNullOrEmpty(decodedTime) || decodedTime.Length < minimumValidLength)
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            if (decodedTime[decodedTime.Length - 1] != 'Z')
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            int year;
            if (!int.TryParse(decodedTime.Substring(0, 4), out year))
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            int month;
            if (!int.TryParse(decodedTime.Substring(4, 2), out month))
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            int day;
            if (!int.TryParse(decodedTime.Substring(6, 2), out day))
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            int hour;
            if (!int.TryParse(decodedTime.Substring(8, 2), out hour))
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            int minute;
            if (!int.TryParse(decodedTime.Substring(10, 2), out minute))
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            int second;
            if (!int.TryParse(decodedTime.Substring(12, 2), out second))
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            // Millisecond accuracy should be enough.
            var milliseconds = 0;

            // Still, we need to verify that the DER-encoded GeneralizedTime value is correct (per RFC 3161).
            if (decodedTime[14] == '.')
            {
                // YYYYMMDDhhmmss.sZ
                minimumValidLength = 17;

                // Disallow YYYYMMDDhhmmss.Z
                if (decodedTime.Length < minimumValidLength)
                {
                    throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
                }

                var hasTrailingZero = false;

                // Skip trailing 'Z'.  It was checked earlier.
                for (var i = 15; i < decodedTime.Length - 1; ++i)
                {
                    var c = decodedTime[i];

                    if (!char.IsNumber(c))
                    {
                        throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
                    }

                    hasTrailingZero = c == '0';
                }

                if (hasTrailingZero)
                {
                    throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
                }

                // Since we're only reading milliseconds, the maximum number of digits to read is 3.
                // The minimum number of digits to read is the count of digits from position 15 to before the 'Z'.
                var digitsToRead = Math.Min(3, decodedTime.Length - 1 - 15);
                var fraction = decodedTime.Substring(15, digitsToRead).PadRight(3, '0');

                milliseconds = int.Parse(fraction);
            }

            DateTime dateTime;

            try
            {
                dateTime = new DateTime(year, month, day, hour, minute, second, milliseconds, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new CryptographicException(SR.Cryptography_Der_Invalid_Encoding);
            }

            return new DerGeneralizedTime(dateTime);
        }
    }
}