// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.ContentModel;
using NuGet.ContentModel.Infrastructure;
using Xunit;

namespace NuGet.Client.Test
{
    public class PatternExpressionMatchTests
    {

        
        [Fact]
        public void LiteralSegment_OnlyShouldMatch()
        {
            // match literal segments given a pattern
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "type", new ContentPropertyDefinition("text") }
            };
            var path = "content/file.txt";
            var pattern = new PatternDefinition(path);
            var expression = new PatternExpression(pattern);
            var result = expression.Match(path, propertyDefinitions);
            Assert.NotNull(result);
            Assert.Equal(path, result.Path);
        }

        [Fact]
        public void LiteralSegment_OnlyShouldNotMatch()
        {
            // should not match the literal segment patern because file.txt != file2.txt
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
        public void LiteralSegment_hasTokenSegmentShouldMatch()
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
            Assert.True(match_result.Properties.TryGetValue("name", out object result));
            Assert.Equal("file.txt", result);
        }

        [Fact]
        public void LiteralSegment_hasTokenSegmentShouldNotMatch()
        {
            // We do not have a parser for the token segment name.
            //As a result it should not be able to match and return a content item
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
                {"name", new ContentPropertyDefinition("any") }
            };
            // this pattern has both a token segment and a literal segment
            var pattern = new PatternDefinition("content/{name}");
            var expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            var matchResult = expression.Match(path, propertyDefinitions);
            Assert.Null(match_result);
        }

        [Fact]
        public void TokenSegment_matchOnly()
        {
            // We have a parser that would match any field however the field "file.txt" here should not be added to the key "name" in the content Item
            // because we used ? in the pattern for the token name and as a result the token is a matchonly token.
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
                {"name", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            //this pattern has just a tokenSegment and it only requires to be matched it won't add a property to the
            // content item object
            var pattern = new PatternDefinition("{name?}");
            var expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            var match_result = expression.Match(path, propertyDefinitions);
            Assert.NotNull(match_result);
            object result;
            match_result.Properties.TryGetValue("name", out result);
            Assert.Null(result);
        }

        [Fact]
        public void TokenSegment_tfmToken()
        {
            // We have a simple parser for tfm tokens and it basically returns whatever was passed to it
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
                {"tfm", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            //this pattern has tfm as a token segment and is not match only
            //As a result tfm_raw property will be saved for the content item token matching with tfm
            var pattern = new PatternDefinition("contentItem/{tfm}");
            var expression = new PatternExpression(pattern);
            var path = "contentItem/this/is/tfm/raw/file.txt";
            var expected_result = "this/is/tfm/raw/file.txt";
            var match_result = expression.Match(path, propertyDefinitions);
            Assert.NotNull(match_result);
            object result;
            match_result.Properties.TryGetValue("tfm_raw", out result);
            Assert.Equal(expected_result, result);
        }

        [Fact]
        public void TokenSegment_multipleTokens()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
        {"name", new ContentPropertyDefinition("any", parser: (o, t) => o) },
        {"version", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            var pattern = new PatternDefinition("contentItem/{name}/{version}");
            var expression = new PatternExpression(pattern);
            var path = "contentItem/mypackage/1.0.0";
            var expected_name = "mypackage";
            var expected_version = "1.0.0";
            var match_result = expression.Match(path, propertyDefinitions);
            Assert.NotNull(match_result);
            object name_result;
            object version_result;
            match_result.Properties.TryGetValue("name", out name_result);
            match_result.Properties.TryGetValue("version", out version_result);
            Assert.Equal(expected_name, name_result);
            Assert.Equal(expected_version, version_result);
        }

        [Fact]
        public void TokenSegment_customDelimiter()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
        {"name", new ContentPropertyDefinition("any", parser: (o, t) => o) },
        {"version", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            var pattern = new PatternDefinition("contentItem/{name}--{version}");
            var expression = new PatternExpression(pattern);
            var path = "contentItem/mypackage--1.0.0/file.dll";
            var expected_result = "mypackage";
            var match_result = expression.Match(path, propertyDefinitions);
            Assert.NotNull(match_result);
            object result;
            match_result.Properties.TryGetValue("name", out result);
            Assert.Equal(expected_result, result);
        }

        [Fact]
        public void TokenSegment_tokenNotFound()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>()
                {
                    // Empty propertyDefinitions, no token definition available
                };
            var pattern = new PatternDefinition("contentItem/{name}");
            var expression = new PatternExpression(pattern);
            var path = "contentItem/mypackage/file.txt";
            Assert.Throws<Exception>(() => expression.Match(path, propertyDefinitions));
        }

        [Fact]
        public void TokenSegment_multipleOccurrences()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions =
                new Dictionary<string, ContentPropertyDefinition>() {
        {"name", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            var pattern = new PatternDefinition("contentItem/{name}/{name}");
            var expression = new PatternExpression(pattern);
            var path = "contentItem/mypackage/file.dll";
            Assert.Throws<ArgumentException>(() => expression.Match(path, propertyDefinitions));
        }



    }
}
