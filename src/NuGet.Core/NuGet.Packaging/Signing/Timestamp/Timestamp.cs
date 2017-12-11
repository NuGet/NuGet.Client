// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

#if IS_DESKTOP
using System.Security.Cryptography.Pkcs;
#endif

namespace NuGet.Packaging.Signing
{
    public sealed class Timestamp
    {
#if IS_DESKTOP

        /// <summary>
        /// Upper limit of Timestamp.
        /// </summary>
        public DateTimeOffset UpperLimit { get; }

        /// <summary>
        /// Lower limit of Timestamp.
        /// </summary>
        public DateTimeOffset LowerLimit { get; }

        /// <summary>
        /// Timestamp SignerInfo.
        /// </summary>
        public SignerInfo SignerInfo { get; }

        /// <summary>
        /// Timestamp information.
        /// </summary>
        /// <param name="timestampSignerInfo">Timestamp SignerInfo.</param>
        /// <param name="upperLimit">Upper limit of Timestamp.</param>
        /// <param name="lowerLimit">Lower limit of Timestamp.</param>
        public Timestamp(SignerInfo timestampSignerInfo, DateTimeOffset upperLimit, DateTimeOffset lowerLimit)
        {
            SignerInfo = timestampSignerInfo ?? throw new ArgumentNullException(nameof(timestampSignerInfo));
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
        }
#endif
    }
}
