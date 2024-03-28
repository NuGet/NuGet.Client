// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.ContentModel
{
    /// <summary>
    /// Represents a comparer of <see cref="ReadOnlyMemory{T}" /> that uses string ordinal comparison.
    /// </summary>
    internal sealed class ReadOnlyMemoryCharComparerOrdinal : IEqualityComparer<ReadOnlyMemory<char>>
    {
        public static ReadOnlyMemoryCharComparerOrdinal Instance { get; } = new ReadOnlyMemoryCharComparerOrdinal();

        private ReadOnlyMemoryCharComparerOrdinal()
        {
        }

        public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
        {
            return x.Span.Equals(y.Span, StringComparison.Ordinal);
        }

        public unsafe int GetHashCode(ReadOnlyMemory<char> obj)
        {
            if (obj.Length == 0)
            {
                return 0;
            }

            fixed (char* pSpan0 = obj.Span)
            {
                int num1 = 0x15051505;
                int num2 = num1;

                int* pSpan = (int*)pSpan0;

                int charactersRemaining;

                for (charactersRemaining = obj.Length; charactersRemaining >= 4; charactersRemaining -= 4)
                {
                    num1 = ((num1 << 5) + num1 + (num1 >> 27)) ^ *pSpan;
                    num2 = ((num2 << 5) + num2 + (num2 >> 27)) ^ pSpan[1];
                    pSpan += 2;
                }

                if (charactersRemaining > 0)
                {
                    num1 = ((num1 << 5) + num1 + (num1 >> 27)) ^ pSpan0[obj.Length - 1];
                }

                return (num1 + (num2 * 0x5D588B65)) & 0x7FFFFFFF;
            }
        }
    }
}
