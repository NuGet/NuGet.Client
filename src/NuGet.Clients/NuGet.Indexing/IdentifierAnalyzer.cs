// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;
using Lucene.Net.Analysis;

namespace NuGet.Indexing
{
    public class IdentifierAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            return new LowerCaseFilter(new CamelCaseFilter(new DotTokenizer(reader)));
        }
    }
}
