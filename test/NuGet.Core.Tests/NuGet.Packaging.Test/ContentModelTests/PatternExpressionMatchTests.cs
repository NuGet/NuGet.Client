// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.ContentModel;
using NuGet.ContentModel.Infrastructure;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PatternExpressionMatchTests
    {
        [Fact]
        public void Match_LiteralSegment_ShouldMatch()
        {
            // match literal segments given a pattern
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "type", new ContentPropertyDefinition("text") }
            };
            var path = "content/file.txt";
            PatternDefinition pattern = new PatternDefinition(path);
            PatternExpression expression = new PatternExpression(pattern);
            ContentItem result = expression.Match(path, propertyDefinitions);
            Assert.NotNull(result);
            Assert.Equal(path, result.Path);
        }

        [Fact]
        public void Match_LiteralSegment_ShouldNotMatch()
        {
            // should not match the literal segment pattern because file.txt != file2.txt
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "type", new ContentPropertyDefinition("text") }
            };
            PatternDefinition pattern = new PatternDefinition("content/file.txt");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "content/file2.txt";
            ContentItem result = expression.Match(path, propertyDefinitions);
            Assert.Null(result);
        }

        [Fact]
        public void Match_LiteralSegmentAndTokenSegment_ShouldMatch()
        {
            //We have a parser for the token "name" which takes in any type of string and returns it.
            //Therefore file.txt should be added to the result content item
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "name", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            //this pattern has both a token segment and a literal segment
            PatternDefinition pattern = new PatternDefinition("content/{name}");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            ContentItem matchResult = expression.Match(path, propertyDefinitions);
            Assert.True(matchResult.Properties.TryGetValue("name", out object result));
            Assert.Equal("file.txt", result);
        }

        [Fact]
        public void Match_LiteralSegmentAndTokenSegment_ShouldNotMatch()
        {
            // We do not have a parser for the token segment name.
            //As a result it should not be able to match and return a content item
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "name", new ContentPropertyDefinition("any") }
            };
            // this pattern has both a token segment and a literal segment
            PatternDefinition pattern = new PatternDefinition("content/{name}");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            ContentItem matchResult = expression.Match(path, propertyDefinitions);
            Assert.Null(matchResult);
        }

        [Fact]
        public void Match_TokenSegmentMatchOnly_ShouldNotAddProperty()
        {
            // We have a parser that would match any field however the field "file.txt" here should not be added to the key "name" in the content Item
            // because we used ? in the pattern for the token name and as a result the token is a matchonly token.
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "name", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            //this pattern has just a tokenSegment and it only requires to be matched it won't add a property to the
            // content item object
            PatternDefinition pattern = new PatternDefinition("{name?}");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "content/file.txt";
            ContentItem matchResult = expression.Match(path, propertyDefinitions);
            Assert.NotNull(matchResult);
            matchResult.Properties.TryGetValue("name", out object result);
            Assert.Null(result);
        }

        [Fact]
        public void Match_TokenSegmentTfmToken_AddsTfmProperty()
        {
            // We have a simple parser for tfm tokens and it basically returns whatever was passed to it
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "tfm", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            //this pattern has tfm as a token segment and is not match only
            //As a result tfm_raw property will be saved for the content item token matching with tfm
            PatternDefinition pattern = new PatternDefinition("contentItem/{tfm}");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "contentItem/this/is/tfm/raw/file.txt";
            var expectedResult = "this/is/tfm/raw/file.txt";
            ContentItem matchResult = expression.Match(path, propertyDefinitions);
            Assert.NotNull(matchResult);
            matchResult.Properties.TryGetValue("tfm_raw", out object result);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void Match_TokenSegmentMultipleTokens_AddsProperties()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "name", new ContentPropertyDefinition("any", parser: (o, t) => o) },
                { "version", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            PatternDefinition pattern = new PatternDefinition("contentItem/{name}/{version}");
            PatternExpression expression = new PatternExpression(pattern);
            var expectedName = "mypackage";
            var expectedVersion = "1.0.0";
            var path = $"contentItem/{expectedName}/{expectedVersion}";
            var matchResult = expression.Match(path, propertyDefinitions);
            Assert.NotNull(matchResult);
            matchResult.Properties.TryGetValue("name", out object nameResult);
            matchResult.Properties.TryGetValue("version", out object versionResult);
            Assert.Equal(expectedName, nameResult);
            Assert.Equal(expectedVersion, versionResult);
        }

        [Fact]
        public void Match_TokenSegmentCustomDelimiter_ShouldMatch()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "name", new ContentPropertyDefinition("any", parser: (o, t) => o) },
                { "version", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            PatternDefinition pattern = new PatternDefinition("contentItem/{name}--{version}");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "contentItem/mypackage--1.0.0/file.dll";
            var expectedResult = "mypackage";
            ContentItem matchResult = expression.Match(path, propertyDefinitions);
            Assert.NotNull(matchResult);
            matchResult.Properties.TryGetValue("name", out object result);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void Match_TokenSegment_TokenNotFound()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
                {
                    // Empty propertyDefinitions, no token definition available
                };
            PatternDefinition pattern = new PatternDefinition("contentItem/{name}");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "contentItem/mypackage/file.txt";
            Assert.Throws<Exception>(() => expression.Match(path, propertyDefinitions));
        }

        [Fact]
        public void Match_TokenSegmentMultipleOccurrences_ThrowsException()
        {
            Dictionary<string, ContentPropertyDefinition> propertyDefinitions = new()
            {
                { "name", new ContentPropertyDefinition("any", parser: (o, t) => o) }
            };
            PatternDefinition pattern = new PatternDefinition("contentItem/{name}/{name}");
            PatternExpression expression = new PatternExpression(pattern);
            var path = "contentItem/mypackage/file.dll";
            ArgumentException exception = Assert.Throws<ArgumentException>(() => expression.Match(path, propertyDefinitions));
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, "An item with the same key has already been added. Key: name"), exception.Message);
        }
    }
}
