// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Licenses
{
    public enum LicenseTokenType
    {
        WITH,
        AND,
        OR,
        OPENING_BRACKET,
        CLOSING_BRACKET,
        VALUE
    }

    public static class Extensions
    {
        public static bool IsOperator(this LicenseTokenType grade)
        {
            return grade == LicenseTokenType.WITH || grade == LicenseTokenType.AND || grade == LicenseTokenType.OR;
        }

        public static bool IsValidPrecedingToken(this LicenseTokenType current, LicenseTokenType precedingToken)
        {
            switch (current)
            {
                case LicenseTokenType.OPENING_BRACKET: // Legal preceding tokens: None, Operator, OpeningBracket
                    return precedingToken.IsOperator() || current == precedingToken;
                case LicenseTokenType.CLOSING_BRACKET: // Legal preceding tokens: ClosingBracket, Value
                    return precedingToken == LicenseTokenType.VALUE || precedingToken == LicenseTokenType.CLOSING_BRACKET;
                case LicenseTokenType.VALUE: // Legal preceding tokens: None, Operator, OpeningBracket
                    return precedingToken.IsOperator() || precedingToken == LicenseTokenType.OPENING_BRACKET;
                case LicenseTokenType.AND: // Legal preceding tokens: Value, ClosingBracket
                case LicenseTokenType.WITH:
                case LicenseTokenType.OR:
                    return precedingToken == LicenseTokenType.VALUE || precedingToken == LicenseTokenType.CLOSING_BRACKET;
                default:
                    return false;
            }
        }
    }
}
