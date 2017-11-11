// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tsp;
using Org.BouncyCastle.X509;

namespace NuGet.Packaging.FuncTest
{
    public class TestTimestampProvider : Rfc3161TimestampProvider
    {
        public TestTimestampProvider(Uri timeStampServerUrl) : base(timeStampServerUrl)
        {
        }

        internal override Rfc3161TimestampToken GetTimestampToken(Rfc3161TimestampRequest rfc3161TimestampRequest)
        {
            var generator = TestTimestampUtility.GenerateTimestampGenerator();
            var bouncyCastleRequest = rfc3161TimestampRequest.BouncyCastleTimestampRequest();
            var token = generator.Generate(bouncyCastleRequest, new Org.BouncyCastle.Math.BigInteger("100"), DateTime.Now);
            return Rfc3161TimestampToken.LoadOnly(token.GetEncoded());
        }
    }
}
