// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using Lucene.Net.Analysis;

namespace NuGet.Indexing
{
    public class DotTokenizer : CharTokenizer
    {
        public DotTokenizer(TextReader input)
            : base(input)
        {
        }

        protected override bool IsTokenChar(char c)
        {
            return !(Char.IsWhiteSpace(c)
                || c == '.'
                || c == '-'
                || c == ','
                || c == ';'
                || c == ':'
                || c == '\''
                || c == '*'
                || c == '#'
                || c == '!'
                || c == '~'
                || c == '+'
                || c == '-'
                || c == '(' || c == ')'
                || c == '[' || c == ']'
                || c == '{' || c == '}');
        }
    }
}
