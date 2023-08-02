// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ContentModel;
using NuGet.ContentModel.Infrastructure;
using Xunit;

namespace NuGet.Client.Test
{
    public class PatternExpressionTests
    {

        
        [Fact]
        public void LiteralSegment_OnlyShouldMatch()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
                {"type", new ContentPropertyDefinition("text") }
            };
            var pattern = new PatternDefinition("content/file.txt");
            var expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            var result = expression.Match(path, propertyDefinitions);
            Assert.NotNull(result);
            Assert.Equal(path, result.Path);
        }

        [Fact]
        public void LiteralSegment_OnlyShouldNotMatch()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
                {"type", new ContentPropertyDefinition("text") }
            };
            var pattern = new PatternDefinition("content/file.txt");
            var expression = new PatternExpression(pattern);
            var path = "content/file2.txt";
            var result = expression.Match(path, propertyDefinitions);
            Assert.Null(result);
        }

        [Fact]
        public void LiteralSegment_AndTokenSegmentShouldMatch()
        {
            //We have a parser for the token "name" which takes in any type of string and returns it.
            //Therefore file.txt should be added to the result content item
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
                {"name", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            //this pattern has both a token segment and a litteral segment
            var pattern = new PatternDefinition("content/{name}");
            var expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            var match_result = expression.Match(path, propertyDefinitions);
            object result;
            match_result.Properties.TryGetValue("name", out result);
            Assert.NotNull(result);
            Assert.Equal("file.txt", result);
        }

        [Fact]
        public void LiteralSegment_AndTokenSegmentShouldNotMatch()
        {
            // We do not have a parser for the token segment name.
            //As a result it should not be able to match and return a content item
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
                {"name", new ContentPropertyDefinition("any") }
            };
            //this pattern has both a token segment and a litteral segment
            var pattern = new PatternDefinition("content/{name}");
            var expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            var match_result = expression.Match(path, propertyDefinitions);
            Assert.Null(match_result);
        }


    }
}
