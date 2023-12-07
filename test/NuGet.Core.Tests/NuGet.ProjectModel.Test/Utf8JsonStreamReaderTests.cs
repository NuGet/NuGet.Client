// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    [UseCulture("")] // Fix tests failing on systems with non-English locales
    public class Utf8JsonStreamReaderTests
    {
        private static readonly string JsonWithOverflowObject = "{\"object1\":{\"a\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"b\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"c\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"d\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"e\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"f\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"g\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"h\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"i\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"j\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"k\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"l\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"m\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"n\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"o\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"p\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"q\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"r\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"s\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"t\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"u\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\"},\"object2\":{\"a\":\"abcdefghijklmnopqrstuvwxyz\",\"b\":\"abcdefghijklmnopqrstuvwxyz\",\"c\":\"abcdefghijklmnopqrstuvwxyz\",\"d\":\"abcdefghijklmnopqrstuvwxyz\",\"e\":\"abcdefghijklmnopqrstuvwxyz\",\"f\":\"abcdefghijklmnopqrstuvwxyz\",\"g\":\"abcdefghijklmnopqrstuvwxyz\",\"h\":\"abcdefghijklmnopqrstuvwxyz\",\"i\":\"abcdefghijklmnopqrstuvwxyz\",\"j\":\"abcdefghijklmnopqrstuvwxyz\",\"k\":\"abcdefghijklmnopqrstuvwxyz\",\"l\":\"abcdefghijklmnopqrstuvwxyz\",\"m\":\"abcdefghijklmnopqrstuvwxyz\",\"n\":\"abcdefghijklmnopqrstuvwxyz\",\"o\":\"abcdefghijklmnopqrstuvwxyz\",\"p\":\"abcdefghijklmnopqrstuvwxyz\",\"q\":\"abcdefghijklmnopqrstuvwxyz\",\"r\":\"abcdefghijklmnopqrstuvwxyz\",\"s\":\"abcdefghijklmnopqrstuvwxyz\",\"t\":\"abcdefghijklmnopqrstuvwxyz\",\"u\":\"abcdefghijklmnopqrstuvwxyz\"}}";
        private static readonly string JsonWithoutOverflow = "{\"object1\":{\"a\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"b\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"c\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"d\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"e\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"f\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"g\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"h\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"i\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"j\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"k\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"l\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"m\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"n\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"o\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"p\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"q\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"r\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"s\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"t\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"u\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\"}}";
        private static readonly string JsonWithOverflow = "{\"object1\":{\"a\":\"abcdefghijklmnopqrstuvwxyz\",\"b\":\"abcdefghijklmnopqrstuvwxyz\",\"c\":\"abcdefghijklmnopqrstuvwxyz\",\"d\":\"abcdefghijklmnopqrstuvwxyz\",\"e\":\"abcdefghijklmnopqrstuvwxyz\",\"f\":\"abcdefghijklmnopqrstuvwxyz\",\"g\":\"abcdefghijklmnopqrstuvwxyz\",\"h\":\"abcdefghijklmnopqrstuvwxyz\",\"i\":\"abcdefghijklmnopqrstuvwxyz\",\"j\":\"abcdefghijklmnopqrstuvwxyz\",\"k\":\"abcdefghijklmnopqrstuvwxyz\",\"l\":\"abcdefghijklmnopqrstuvwxyz\",\"m\":\"abcdefghijklmnopqrstuvwxyz\",\"n\":\"abcdefghijklmnopqrstuvwxyz\",\"o\":\"abcdefghijklmnopqrstuvwxyz\",\"p\":\"abcdefghijklmnopqrstuvwxyz\",\"q\":\"abcdefghijklmnopqrstuvwxyz\",\"r\":\"abcdefghijklmnopqrstuvwxyz\",\"s\":\"abcdefghijklmnopqrstuvwxyz\",\"t\":\"abcdefghijklmnopqrstuvwxyz\",\"u\":\"abcdefghijklmnopqrstuvwxyz\"}, \"object2\": {\"a\":\"abcdefghijklmnopqrstuvwxyz\",\"b\":\"abcdefghijklmnopqrstuvwxyz\",\"c\":\"abcdefghijklmnopqrstuvwxyz\",\"d\":\"abcdefghijklmnopqrstuvwxyz\",\"e\":\"abcdefghijklmnopqrstuvwxyz\",\"f\":\"abcdefghijklmnopqrstuvwxyz\",\"g\":\"abcdefghijklmnopqrstuvwxyz\",\"h\":\"abcdefghijklmnopqrstuvwxyz\",\"i\":\"abcdefghijklmnopqrstuvwxyz\",\"j\":\"abcdefghijklmnopqrstuvwxyz\",\"k\":\"abcdefghijklmnopqrstuvwxyz\",\"l\":\"abcdefghijklmnopqrstuvwxyz\",\"m\":\"abcdefghijklmnopqrstuvwxyz\",\"n\":\"abcdefghijklmnopqrstuvwxyz\",\"o\":\"abcdefghijklmnopqrstuvwxyz\",\"p\":\"abcdefghijklmnopqrstuvwxyz\",\"q\":\"abcdefghijklmnopqrstuvwxyz\",\"r\":\"abcdefghijklmnopqrstuvwxyz\",\"s\":\"abcdefghijklmnopqrstuvwxyz\",\"t\":\"abcdefghijklmnopqrstuvwxyz\",\"u\":\"abcdefghijklmnopqrstuvwxyz\"}, \"object3\":{\"a\":\"abcdefghijklmnopqrstuvwxyz\",\"b\":\"abcdefghijklmnopqrstuvwxyz\",\"c\":\"abcdefghijklmnopqrstuvwxyz\",\"d\":\"abcdefghijklmnopqrstuvwxyz\",\"e\":\"abcdefghijklmnopqrstuvwxyz\",\"f\":\"abcdefghijklmnopqrstuvwxyz\",\"g\":\"abcdefghijklmnopqrstuvwxyz\",\"h\":\"abcdefghijklmnopqrstuvwxyz\",\"i\":\"abcdefghijklmnopqrstuvwxyz\",\"j\":\"abcdefghijklmnopqrstuvwxyz\",\"k\":\"abcdefghijklmnopqrstuvwxyz\",\"l\":\"abcdefghijklmnopqrstuvwxyz\",\"m\":\"abcdefghijklmnopqrstuvwxyz\",\"n\":\"abcdefghijklmnopqrstuvwxyz\",\"o\":\"abcdefghijklmnopqrstuvwxyz\",\"p\":\"abcdefghijklmnopqrstuvwxyz\",\"q\":\"abcdefghijklmnopqrstuvwxyz\",\"r\":\"abcdefghijklmnopqrstuvwxyz\",\"s\":\"abcdefghijklmnopqrstuvwxyz\",\"t\":\"abcdefghijklmnopqrstuvwxyz\",\"u\":\"abcdefghijklmnopqrstuvwxyz\"}}";

        [Fact]
        public void Utf8JsonStreamReaderCtr_WhenStreamIsNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Utf8JsonStreamReader(null));
        }

        [Fact]
        public void Utf8JsonStreamReaderCtr_WhenStreamStartsWithUtf8Bom_SkipThem()
        {
            var json = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble()) + "{}";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var reader = new Utf8JsonStreamReader(stream);
                Assert.Equal(5, stream.Position);
                Assert.Equal(JsonTokenType.StartObject, reader.TokenType);
            }
        }

        [Fact]
        public void Utf8JsonStreamReaderCtr_WhenStreamStartsWithoutUtf8Bom_ReadFromStart()
        {
            var json = "{}";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var reader = new Utf8JsonStreamReader(stream);
                Assert.Equal(2, stream.Position);
                Assert.Equal(JsonTokenType.StartObject, reader.TokenType);
            }
        }

        [Fact]
        public void Utf8JsonStreamReaderCtr_WhenReadingWithOverflow_BufferHasFirst1024Bytes()
        {
            var json = Encoding.UTF8.GetBytes(JsonWithOverflowObject);
            var firstBytes = json.Take(1024).ToArray();

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);
                var bufferString = reader.GetCurrentBufferAsString();
                Assert.Equal(Encoding.UTF8.GetString(firstBytes), bufferString);
            }
        }

        [Fact]
        public void Read_WhenReadingWithoutOverflow_Read()
        {
            var json = Encoding.UTF8.GetBytes(JsonWithoutOverflow);

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);
                Assert.Equal(JsonTokenType.StartObject, reader.TokenType);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
            }
        }

        [Fact]
        public void Read_WhenReadingWithOverflow_ReadNextBuffer()
        {
            var json = Encoding.UTF8.GetBytes(JsonWithOverflowObject);

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "r")
                    {
                        break;
                    }
                }
                reader.Read();

                Assert.True(reader.IsFinalBlock);
                Assert.Equal(1024, reader.BufferSize);
                Assert.Equal(JsonTokenType.String, reader.TokenType);
                Assert.Equal("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", reader.GetString());
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
                Assert.Equal("s", reader.GetString());
            }
        }

        [Fact]
        public void Read_WhenReadingWithLargeToken_ResizeBuffer()
        {
            var json = Encoding.UTF8.GetBytes("{\"largeToken\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\",\"smallToken\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\"}");

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);

                reader.Read();
                reader.Read();

                Assert.Equal(JsonTokenType.String, reader.TokenType);
                Assert.Equal("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz",
                    reader.GetString());
                Assert.Equal(2048, reader.BufferSize);
            }
        }

        [Fact]
        public void Read_WhenReadingWithOverflowToBufferSize_LoadNextBuffer()
        {
            var json = Encoding.UTF8.GetBytes("{\"largeToken\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrst\",\"smallToken\":\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\"}");

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);

                reader.Read();
                reader.Read();
                reader.Read();

                Assert.True(reader.IsFinalBlock);
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
                Assert.Equal("smallToken", reader.GetString());
                Assert.True(reader.Read());
                Assert.Equal(JsonTokenType.String, reader.TokenType);
                Assert.Equal("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", reader.GetString());
            }
        }

        [Fact]
        public void TrySkip_WhenReadingWithoutOverflow_SkipObject()
        {
            var json = Encoding.UTF8.GetBytes(JsonWithoutOverflow);

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);
                Assert.Equal(JsonTokenType.StartObject, reader.TokenType);
                reader.TrySkip();
                Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
            }
        }

        [Fact]
        public void TrySkip_WhenReadingWithOverflow_Skip()
        {
            var json = Encoding.UTF8.GetBytes(JsonWithOverflow);

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                reader.TrySkip();
                reader.Read();
                reader.TrySkip();
                Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
                reader.Read();
                Assert.Equal("object3", reader.GetString());
                Assert.Equal(1024, reader.BufferSize);

            }
        }

        [Fact]
        public void TrySkip_WhenReadingWithOverflowObject_ResizeBuffer()
        {
            var json = Encoding.UTF8.GetBytes(JsonWithOverflowObject);

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);

                reader.Read();
                reader.TrySkip();
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
                Assert.Equal("object2", reader.GetString());
                Assert.Equal(2048, reader.BufferSize);
            }
        }

        [Fact]
        public void ReadNextTokenAsString_WhenCalled_AdvanceToken()
        {
            var json = Encoding.UTF8.GetBytes("{\"token\":\"value\"}");

            using (var stream = new MemoryStream(json))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
                reader.ReadNextTokenAsString();
                Assert.Equal(JsonTokenType.String, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("null")]
        [InlineData("\"b\"")]
        [InlineData("{}")]
        public void ReadStringArrayIntoList_WhenValueIsNotArray_ReturnsNull(string value)
        {
            var json = $"{{\"a\":{value}}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                reader.Read();
                Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
                List<string> list = null;
                reader.ReadStringArrayIntoList(list);
                Assert.Null(list);
            }
        }

        [Fact]
        public void ReadStringArrayIntoList_WhenValueIsEmptyArray_ReturnsNull()
        {
            var encodedBytes = Encoding.UTF8.GetBytes("{\"a\":[]}");
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                reader.Read();
                Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
                List<string> list = null;
                reader.ReadStringArrayIntoList(list);
                Assert.Null(list);
            }
        }

        [Fact]
        public void ReadStringArrayIntoList_WithSupportedTypes_ReturnsStringArray()
        {
            var encodedBytes = Encoding.UTF8.GetBytes("[\"a\",-2,3.14,true,null]");
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                List<string> actualValues = null;
                reader.ReadStringArrayIntoList(actualValues);

                Assert.Collection(
                    actualValues,
                    actualValue => Assert.Equal("a", actualValue),
                    actualValue => Assert.Equal("-2", actualValue),
                    actualValue => Assert.Equal("3.14", actualValue),
                    actualValue => Assert.Equal("True", actualValue),
                    actualValue => Assert.Equal(null, actualValue));
                Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("{}")]
        public void ReadStringArrayIntoList_WithUnsupportedTypes_Throws(string element)
        {
            var encodedBytes = Encoding.UTF8.GetBytes($"[{element}]");
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                Exception exceptionThrown = null;
                try
                {
                    reader.ReadStringArrayIntoList();
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }
                Assert.IsType(typeof(InvalidCastException), exceptionThrown);
            }
        }


        [Theory]
        [InlineData("true", JsonTokenType.True)]
        [InlineData("false", JsonTokenType.False)]
        [InlineData("-2", JsonTokenType.Number)]
        [InlineData("3.14", JsonTokenType.Number)]
        [InlineData("{}", JsonTokenType.StartObject)]
        [InlineData("[]", JsonTokenType.StartArray)]
        [InlineData("[true]", JsonTokenType.StartArray)]
        [InlineData("[-2]", JsonTokenType.StartArray)]
        [InlineData("[3.14]", JsonTokenType.StartArray)]
        [InlineData("[\"a\", \"b\"]", JsonTokenType.StartArray)]

        public void ReadDelimitedString_WhenValueIsNotString_Throws(string value, JsonTokenType expectedTokenType)
        {
            var json = $"{{\"a\":{value}}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();

                Exception exceptionThrown = null;
                try
                {
                    reader.ReadDelimitedString();
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }

                Assert.NotNull(exceptionThrown);
                Assert.IsType(typeof(JsonException), exceptionThrown);
                Assert.NotNull(exceptionThrown.InnerException);
                Assert.IsType(typeof(InvalidCastException), exceptionThrown.InnerException);
                Assert.Equal(expectedTokenType, reader.TokenType);
            }
        }

        [Fact]
        public void ReadDelimitedString_WhenValueIsString_ReturnsValue()
        {
            const string expectedResult = "b";
            var json = $"{{\"a\":\"{expectedResult}\"}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                IEnumerable<string> actualResults = reader.ReadDelimitedString();
                Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
                Assert.Equal(JsonTokenType.String, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("b,c,d")]
        [InlineData("b c d")]
        public void ReadDelimitedString_WhenValueIsDelimitedString_ReturnsValues(string value)
        {
            string[] expectedResults = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var json = $"{{\"a\":\"{value}\"}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                IEnumerable<string> actualResults = reader.ReadDelimitedString();
                Assert.Equal(expectedResults, actualResults);
                Assert.Equal(JsonTokenType.String, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("null")]
        [InlineData("\"b\"")]
        [InlineData("{}")]
        public void ReadStringArrayAsList_WhenValueIsNotArray_ReturnsNull(string value)
        {
            var json = $"{{\"a\":{value}}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                reader.Read();
                Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
                List<string> actualValues = reader.ReadStringArrayAsList();
                Assert.Null(actualValues);
            }
        }

        [Fact]
        public void ReadStringArrayAsList_WhenValueIsEmptyArray_ReturnsNull()
        {
            var encodedBytes = Encoding.UTF8.GetBytes("{\"a\":[]}");
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                reader.Read();
                Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
                List<string> actualValues = reader.ReadStringArrayAsList();
                Assert.Null(actualValues);
            }
        }

        [Fact]
        public void ReadStringArrayAsList_WithSupportedTypes_ReturnsStringArray()
        {
            var encodedBytes = Encoding.UTF8.GetBytes("[\"a\",-2,3.14,true,null]");
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);

                List<string> actualValues = reader.ReadStringArrayAsList();

                Assert.Collection(
                    actualValues,
                    actualValue => Assert.Equal("a", actualValue),
                    actualValue => Assert.Equal("-2", actualValue),
                    actualValue => Assert.Equal("3.14", actualValue),
                    actualValue => Assert.Equal("True", actualValue),
                    actualValue => Assert.Equal(null, actualValue));
                Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("{}")]
        public void ReadStringArrayAsList_WithUnsupportedTypes_Throws(string element)
        {
            var encodedBytes = Encoding.UTF8.GetBytes($"[{element}]");
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);

                Exception exceptionThrown = null;
                try
                {
                    reader.ReadStringArrayAsList();
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }
                Assert.IsType(typeof(InvalidCastException), exceptionThrown);
            }
        }


        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void ReadNextTokenAsBoolOrFalse_WithValidValues_ReturnsBoolean(string value, bool expectedResult)
        {
            var json = $"{{\"a\":{value}}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                bool actualResult = reader.ReadNextTokenAsBoolOrFalse();
                Assert.Equal(expectedResult, actualResult);
            }
        }

        [Theory]
        [InlineData("\"words\"")]
        [InlineData("-3")]
        [InlineData("3.3")]
        [InlineData("[]")]
        [InlineData("{}")]
        public void ReadNextTokenAsBoolOrFalse_WithInvalidValues_ReturnsFalse(string value)
        {
            var json = $"{{\"a\":{value}}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                bool actualResult = reader.ReadNextTokenAsBoolOrFalse();
                Assert.False(actualResult);
            }
        }

        [Fact]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsNull_ReturnsNull()
        {
            const string json = "{\"a\":null}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                Assert.Null(actualResults);
                Assert.Equal(JsonTokenType.Null, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("true", JsonTokenType.True)]
        [InlineData("false", JsonTokenType.False)]
        [InlineData("-2", JsonTokenType.Number)]
        [InlineData("3.14", JsonTokenType.Number)]
        [InlineData("{}", JsonTokenType.StartObject)]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsNotString_ReturnsNull(
            string value,
            JsonTokenType expectedTokenType)
        {
            var json = $"{{\"a\":{value}}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

                Assert.Null(actualResults);
                Assert.Equal(expectedTokenType, reader.TokenType);
            }
        }

        [Fact]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsString_ReturnsValue()
        {
            const string expectedResult = "b";
            var json = $"{{\"a\":\"{expectedResult}\"}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

                Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
                Assert.Equal(JsonTokenType.String, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("b,c,d")]
        [InlineData("b c d")]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsDelimitedString_ReturnsValue(string expectedResult)
        {
            var json = $"{{\"a\":\"{expectedResult}\"}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

                Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
                Assert.Equal(JsonTokenType.String, reader.TokenType);
            }
        }

        [Fact]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsEmptyArray_ReturnsEmptyList()
        {
            const string json = "{\"a\":[]}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                IReadOnlyList<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

                Assert.Empty(actualResults);
                Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("true", "True")]
        [InlineData("-2", "-2")]
        [InlineData("3.14", "3.14")]
        [InlineData("\"b\"", "b")]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsConvertibleToString_ReturnsValueAsString(
            string value,
            string expectedResult)
        {
            var json = $"{{\"a\":[{value}]}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();

                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

                Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
                Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("[]", JsonTokenType.StartArray)]
        [InlineData("{}", JsonTokenType.StartObject)]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsNotConvertibleToString_ReturnsValueAsString(
            string value,
            JsonTokenType expectedToken)
        {
            var json = $"{{\"a\":[{value}]}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                Exception exceptionThrown = null;
                try
                {
                    reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }
                Assert.NotNull(exceptionThrown);
                Assert.IsType(typeof(InvalidCastException), exceptionThrown);
                Assert.Equal(expectedToken, reader.TokenType);
            }
        }

        [Fact]
        public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsArrayOfStrings_ReturnsValues()
        {
            string[] expectedResults = { "b", "c" };
            var json = $"{{\"a\":[{string.Join(",", expectedResults.Select(expectedResult => $"\"{expectedResult}\""))}]}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

                Assert.Equal(expectedResults, actualResults);
                Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
            }
        }

        [Fact]
        public void ReadStringArrayAsReadOnlyListFromArrayStart_WhenValuesAreConvertibleToString_ReturnsReadOnlyList()
        {
            const string json = "[null, true, -2, 3.14, \"a\"]";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

                IEnumerable<string> actualResults = reader.ReadStringArrayAsReadOnlyListFromArrayStart();

                Assert.Collection(
                    actualResults,
                    actualResult => Assert.Equal(null, actualResult),
                    actualResult => Assert.Equal("True", actualResult),
                    actualResult => Assert.Equal("-2", actualResult),
                    actualResult => Assert.Equal("3.14", actualResult),
                    actualResult => Assert.Equal("a", actualResult));
                Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
            }
        }

        [Theory]
        [InlineData("[]", JsonTokenType.StartArray)]
        [InlineData("{}", JsonTokenType.StartObject)]
        public void ReadStringArrayAsReadOnlyListFromArrayStart_WhenValuesAreNotConvertibleToString_Throws(
            string value,
            JsonTokenType expectedToken)
        {
            var json = $"[{value}]";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(encodedBytes))
            {
                var reader = new Utf8JsonStreamReader(stream);
                Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

                Exception exceptionThrown = null;
                try
                {
                    reader.ReadStringArrayAsReadOnlyListFromArrayStart();
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }
                Assert.NotNull(exceptionThrown);
                Assert.IsType(typeof(InvalidCastException), exceptionThrown);
                Assert.Equal(expectedToken, reader.TokenType);
            }
        }
    }
}
