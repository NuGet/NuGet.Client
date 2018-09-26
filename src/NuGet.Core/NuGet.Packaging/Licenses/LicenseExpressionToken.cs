// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Licenses
{
    /// <summary>
    /// Represents a token of a parsed license expression. The tokens are either operators, parentheses or values. 
    /// </summary>
    public class LicenseExpressionToken
    {
        /// <summary>
        /// The token type
        /// </summary>
        public LicenseTokenType TokenType { get; }
        /// <summary>
        /// The value of this token.
        /// </summary>
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
