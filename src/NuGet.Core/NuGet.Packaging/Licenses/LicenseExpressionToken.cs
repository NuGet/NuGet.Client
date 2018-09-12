// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Licenses
{
    public class LicenseExpressionToken
    {
        public LicenseTokenType TokenType { get; }
        public string Value { get; }

        public LicenseExpressionToken(string value, LicenseTokenType tokenType)
        {
            Value = value;
            TokenType = tokenType;
        }

        public override string ToString()
        {
            return $"Value:{Value}, Type: {TokenType}";
        }
    }
}
