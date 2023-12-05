// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Text.Json;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    [UseCulture("")] // Fix tests failing on systems with non-English locales
    public class Utf8JsonReaderExtensionsTests
    {
        //[Fact]
        //public void ReadDelimitedString_WhenValueIsNull_Throws()
        //{
        //    var json = Encoding.UTF8.GetBytes("{\"a\":null}");

        //    var reader = new Utf8JsonReader(json);
        //    reader.Read();
        //    reader.Read();
        //    Exception exceptionThrown = null;
        //    try
        //    {
        //        reader.ReadDelimitedString();
        //    }
        //    catch (Exception ex)
        //    {
        //        exceptionThrown = ex;
        //    }
        //    Assert.NotNull(exceptionThrown);
        //    Assert.IsType(typeof(JsonException), exceptionThrown);
        //    Assert.NotNull(exceptionThrown.InnerException);
        //    Assert.IsType(typeof(InvalidCastException), exceptionThrown.InnerException);
        //    Assert.Equal(JsonTokenType.Null, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("true", JsonTokenType.True)]
        //[InlineData("false", JsonTokenType.False)]
        //[InlineData("-2", JsonTokenType.Number)]
        //[InlineData("3.14", JsonTokenType.Number)]
        //[InlineData("{}", JsonTokenType.StartObject)]
        //[InlineData("[]", JsonTokenType.StartArray)]
        //[InlineData("[true]", JsonTokenType.StartArray)]
        //[InlineData("[-2]", JsonTokenType.StartArray)]
        //[InlineData("[3.14]", JsonTokenType.StartArray)]
        //[InlineData("[\"a\", \"b\"]", JsonTokenType.StartArray)]

        //public void ReadDelimitedString_WhenValueIsNotString_Throws(string value, JsonTokenType expectedTokenType)
        //{
        //    var json = $"{{\"a\":{value}}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();

        //    Exception exceptionThrown = null;
        //    try
        //    {
        //        reader.ReadDelimitedString();
        //    }
        //    catch (Exception ex)
        //    {
        //        exceptionThrown = ex;
        //    }

        //    Assert.NotNull(exceptionThrown);
        //    Assert.IsType(typeof(JsonException), exceptionThrown);
        //    Assert.NotNull(exceptionThrown.InnerException);
        //    Assert.IsType(typeof(InvalidCastException), exceptionThrown.InnerException);
        //    Assert.Equal(expectedTokenType, reader.TokenType);
        //}

        //[Fact]
        //public void ReadDelimitedString_WhenValueIsString_ReturnsValue()
        //{
        //    const string expectedResult = "b";
        //    var json = $"{{\"a\":\"{expectedResult}\"}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    IEnumerable<string> actualResults = reader.ReadDelimitedString();
        //    Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
        //    Assert.Equal(JsonTokenType.String, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("b,c,d")]
        //[InlineData("b c d")]
        //public void ReadDelimitedString_WhenValueIsDelimitedString_ReturnsValues(string value)
        //{
        //    string[] expectedResults = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        //    var json = $"{{\"a\":\"{value}\"}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    IEnumerable<string> actualResults = reader.ReadDelimitedString();
        //    Assert.Equal(expectedResults, actualResults);
        //    Assert.Equal(JsonTokenType.String, reader.TokenType);
        //}

        //[Fact]
        //public void ReadNextToken_WhenAtEndOfStream_ReturnsFalse()
        //{
        //    var encodedBytes = Encoding.UTF8.GetBytes("{}");
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    Assert.True(reader.ReadNextToken());
        //    Assert.True(reader.ReadNextToken());
        //    Assert.False(reader.ReadNextToken());
        //    Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
        //}

        [Theory]
        [InlineData("null", null)]
        [InlineData("true", "True")]
        [InlineData("false", "False")]
        [InlineData("-2", "-2")]
        [InlineData("3.14", "3.14")]
        [InlineData("\"b\"", "b")]
        public void ReadTokenAsString_WhenValueIsConvertibleToString_ReturnsValueAsString(
            string value,
            string expectedResult)
        {
            var json = $"{{\"a\":{value}}}";
            var encodedBytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(encodedBytes);
            reader.Read();
            reader.Read();
            reader.Read();
            string actualResult = reader.ReadTokenAsString();
            Assert.Equal(expectedResult, actualResult);
        }

        //[Theory]
        //[InlineData("[]")]
        //[InlineData("{}")]
        //public void ReadTokenAsString_WhenValueIsNotConvertibleToString_Throws(string value)
        //{
        //    var json = $"{{\"a\":{value}}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    reader.Read();
        //    Exception exceptionThrown = null;
        //    try
        //    {
        //        reader.ReadNextTokenAsString();
        //    }
        //    catch (Exception ex)
        //    {
        //        exceptionThrown = ex;
        //    }
        //    Assert.IsType(typeof(InvalidCastException), exceptionThrown);
        //}

        //[Theory]
        //[InlineData("true", true)]
        //[InlineData("false", false)]
        //public void ReadNextTokenAsBoolOrFalse_WithValidValues_ReturnsBoolean(string value, bool expectedResult)
        //{
        //    var json = $"{{\"a\":{value}}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    bool actualResult = reader.ReadNextTokenAsBoolOrFalse();
        //    Assert.Equal(expectedResult, actualResult);
        //}

        //[Theory]
        //[InlineData("\"words\"")]
        //[InlineData("-3")]
        //[InlineData("3.3")]
        //[InlineData("[]")]
        //[InlineData("{}")]
        //public void ReadNextTokenAsBoolOrFalse_WithInvalidValues_ReturnsFalse(string value)
        //{
        //    var json = $"{{\"a\":{value}}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    bool actualResult = reader.ReadNextTokenAsBoolOrFalse();
        //    Assert.False(actualResult);
        //}

        //[Fact]
        //public void ReadObjectAsList_WithoutStartObject_ReturnEmptyList()
        //{
        //    var json = "[]";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    IList<string> actualResults = reader.ReadObjectAsList<string>(new JsonSerializerOptions());
        //    Assert.Empty(actualResults);
        //}

        //[Fact]
        //public void ReadObjectAsList_WithEmptyObject_ReturnEmptyList()
        //{
        //    var json = "{}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    IList<string> actualResults = reader.ReadObjectAsList<string>(new JsonSerializerOptions());
        //    Assert.Empty(actualResults);
        //}

        //[Theory]
        //[InlineData("null")]
        //[InlineData("\"b\"")]
        //[InlineData("{}")]
        //public void ReadStringArrayAsList_WhenValueIsNotArray_ReturnsNull(string value)
        //{
        //    var json = $"{{\"a\":{value}}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    reader.Read();
        //    Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
        //    List<string> actualValues = reader.ReadStringArrayAsList();
        //    Assert.Null(actualValues);
        //}

        //[Fact]
        //public void ReadStringArrayAsList_WhenValueIsEmptyArray_ReturnsNull()
        //{
        //    var encodedBytes = Encoding.UTF8.GetBytes("{\"a\":[]}");
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    reader.Read();
        //    Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
        //    List<string> actualValues = reader.ReadStringArrayAsList();
        //    Assert.Null(actualValues);
        //}

        //[Fact]
        //public void ReadStringArrayAsList_WithSupportedTypes_ReturnsStringArray()
        //{
        //    var encodedBytes = Encoding.UTF8.GetBytes("[\"a\",-2,3.14,true,null]");
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();

        //    List<string> actualValues = reader.ReadStringArrayAsList();

        //    Assert.Collection(
        //        actualValues,
        //        actualValue => Assert.Equal("a", actualValue),
        //        actualValue => Assert.Equal("-2", actualValue),
        //        actualValue => Assert.Equal("3.14", actualValue),
        //        actualValue => Assert.Equal("True", actualValue),
        //        actualValue => Assert.Equal(null, actualValue));
        //    Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("[]")]
        //[InlineData("{}")]
        //public void ReadStringArrayAsList_WithUnsupportedTypes_Throws(string element)
        //{
        //    var encodedBytes = Encoding.UTF8.GetBytes($"[{element}]");
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    Exception exceptionThrown = null;
        //    try
        //    {
        //        reader.ReadStringArrayAsList();
        //    }
        //    catch (Exception ex)
        //    {
        //        exceptionThrown = ex;
        //    }
        //    Assert.IsType(typeof(InvalidCastException), exceptionThrown);
        //}

        //[Theory]
        //[InlineData("null")]
        //[InlineData("\"b\"")]
        //[InlineData("{}")]
        //public void ReadStringArrayAsIList_WhenValueIsNotArray_ReturnsNull(string value)
        //{
        //    var json = $"{{\"a\":{value}}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    reader.Read();
        //    Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
        //    IList<string> actualValues = reader.ReadStringArrayAsIList();
        //    Assert.Null(actualValues);
        //}

        //[Fact]
        //public void ReadStringArrayAsIList_WhenValueIsEmptyArray_ReturnsNull()
        //{
        //    var encodedBytes = Encoding.UTF8.GetBytes("{\"a\":[]}");
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    reader.Read();
        //    Assert.NotEqual(JsonTokenType.PropertyName, reader.TokenType);
        //    IList<string> actualValues = reader.ReadStringArrayAsIList();
        //    Assert.Null(actualValues);
        //}

        //[Fact]
        //public void ReadStringArrayAsIList_WithSupportedTypes_ReturnsStringArray()
        //{
        //    var encodedBytes = Encoding.UTF8.GetBytes("[\"a\",-2,3.14,true,null]");
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();

        //    IList<string> actualValues = reader.ReadStringArrayAsIList();

        //    Assert.Collection(
        //        actualValues,
        //        actualValue => Assert.Equal("a", actualValue),
        //        actualValue => Assert.Equal("-2", actualValue),
        //        actualValue => Assert.Equal("3.14", actualValue),
        //        actualValue => Assert.Equal("True", actualValue),
        //        actualValue => Assert.Equal(null, actualValue));
        //    Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("[]")]
        //[InlineData("{}")]
        //public void ReadStringArrayAsIList_WithUnsupportedTypes_Throws(string element)
        //{
        //    var encodedBytes = Encoding.UTF8.GetBytes($"[{element}]");
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    Exception exceptionThrown = null;
        //    try
        //    {
        //        reader.ReadStringArrayAsIList();
        //    }
        //    catch (Exception ex)
        //    {
        //        exceptionThrown = ex;
        //    }
        //    Assert.IsType(typeof(InvalidCastException), exceptionThrown);
        //}

        //[Fact]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsNull_ReturnsNull()
        //{
        //    const string json = "{\"a\":null}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
        //    Assert.Null(actualResults);
        //    Assert.Equal(JsonTokenType.Null, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("true", JsonTokenType.True)]
        //[InlineData("false", JsonTokenType.False)]
        //[InlineData("-2", JsonTokenType.Number)]
        //[InlineData("3.14", JsonTokenType.Number)]
        //[InlineData("{}", JsonTokenType.StartObject)]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsNotString_ReturnsNull(
        //    string value,
        //    JsonTokenType expectedTokenType)
        //{
        //    var json = $"{{\"a\":{value}}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        //    IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

        //    Assert.Null(actualResults);
        //    Assert.Equal(expectedTokenType, reader.TokenType);
        //}

        //[Fact]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsString_ReturnsValue()
        //{
        //    const string expectedResult = "b";
        //    var json = $"{{\"a\":\"{expectedResult}\"}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        //    IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

        //    Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
        //    Assert.Equal(JsonTokenType.String, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("b,c,d")]
        //[InlineData("b c d")]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsDelimitedString_ReturnsValue(string expectedResult)
        //{
        //    var json = $"{{\"a\":\"{expectedResult}\"}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        //    IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

        //    Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
        //    Assert.Equal(JsonTokenType.String, reader.TokenType);
        //}

        //[Fact]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsEmptyArray_ReturnsEmptyList()
        //{
        //    const string json = "{\"a\":[]}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        //    IReadOnlyList<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

        //    Assert.Empty(actualResults);
        //    Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("null", null)]
        //[InlineData("true", "True")]
        //[InlineData("-2", "-2")]
        //[InlineData("3.14", "3.14")]
        //[InlineData("\"b\"", "b")]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsConvertibleToString_ReturnsValueAsString(
        //    string value,
        //    string expectedResult)
        //{
        //    var json = $"{{\"a\":[{value}]}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();

        //    Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        //    IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

        //    Assert.Collection(actualResults, actualResult => Assert.Equal(expectedResult, actualResult));
        //    Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("[]", JsonTokenType.StartArray)]
        //[InlineData("{}", JsonTokenType.StartObject)]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsNotConvertibleToString_ReturnsValueAsString(
        //    string value,
        //    JsonTokenType expectedToken)
        //{
        //    var json = $"{{\"a\":[{value}]}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        //    Exception exceptionThrown = null;
        //    try
        //    {
        //        reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
        //    }
        //    catch (Exception ex)
        //    {
        //        exceptionThrown = ex;
        //    }
        //    Assert.NotNull(exceptionThrown);
        //    Assert.IsType(typeof(InvalidCastException), exceptionThrown);
        //    Assert.Equal(expectedToken, reader.TokenType);
        //}

        //[Fact]
        //public void ReadNextStringOrArrayOfStringsAsReadOnlyList_WhenValueIsArrayOfStrings_ReturnsValues()
        //{
        //    string[] expectedResults = { "b", "c" };
        //    var json = $"{{\"a\":[{string.Join(",", expectedResults.Select(expectedResult => $"\"{expectedResult}\""))}]}}";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        //    IEnumerable<string> actualResults = reader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

        //    Assert.Equal(expectedResults, actualResults);
        //    Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        //}

        //[Fact]
        //public void ReadStringArrayAsReadOnlyListFromArrayStart_WhenValuesAreConvertibleToString_ReturnsReadOnlyList()
        //{
        //    const string json = "[null, true, -2, 3.14, \"a\"]";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        //    IEnumerable<string> actualResults = reader.ReadStringArrayAsReadOnlyListFromArrayStart();

        //    Assert.Collection(
        //        actualResults,
        //        actualResult => Assert.Equal(null, actualResult),
        //        actualResult => Assert.Equal("True", actualResult),
        //        actualResult => Assert.Equal("-2", actualResult),
        //        actualResult => Assert.Equal("3.14", actualResult),
        //        actualResult => Assert.Equal("a", actualResult));
        //    Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        //}

        //[Theory]
        //[InlineData("[]", JsonTokenType.StartArray)]
        //[InlineData("{}", JsonTokenType.StartObject)]
        //public void ReadStringArrayAsReadOnlyListFromArrayStart_WhenValuesAreNotConvertibleToString_Throws(
        //    string value,
        //    JsonTokenType expectedToken)
        //{
        //    var json = $"[{value}]";
        //    var encodedBytes = Encoding.UTF8.GetBytes(json);
        //    var reader = new Utf8JsonReader(encodedBytes);
        //    reader.Read();
        //    Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        //    Exception exceptionThrown = null;
        //    try
        //    {
        //        reader.ReadStringArrayAsReadOnlyListFromArrayStart();
        //    }
        //    catch (Exception ex)
        //    {
        //        exceptionThrown = ex;
        //    }
        //    Assert.NotNull(exceptionThrown);
        //    Assert.IsType(typeof(InvalidCastException), exceptionThrown);
        //    Assert.Equal(expectedToken, reader.TokenType);
        //}
    }
}
