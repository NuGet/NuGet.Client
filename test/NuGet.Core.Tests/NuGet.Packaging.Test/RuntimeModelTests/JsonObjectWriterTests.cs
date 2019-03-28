// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.RuntimeModel.Test
{
    public class JsonObjectWriterTests
    {
        private readonly JsonObjectWriter _writer;

        public JsonObjectWriterTests()
        {
            _writer = new JsonObjectWriter();
        }

        [Fact]
        public void GetJson_HasDefaultValue()
        {
            var actualResult = _writer.GetJson();

            Assert.Equal("{}", actualResult);
        }

        [Fact]
        public void GetJson()
        {
            _writer.WriteNameValue("a", 1);
            _writer.WriteNameValue("B", "C");
            _writer.WriteNameArray("d", new[] { "e", "f" });

            _writer.WriteObjectStart("g");
            _writer.WriteNameValue("h", "i");
            _writer.WriteObjectEnd();

            const string expectedJson = @"{
  ""a"": 1,
  ""B"": ""C"",
  ""d"": [
    ""e"",
    ""f""
  ],
  ""g"": {
    ""h"": ""i""
  }
}";
            var actualJson = _writer.GetJson();

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void GetJObject()
        {
            _writer.WriteNameValue("a", 1);
            _writer.WriteNameValue("B", "C");
            _writer.WriteNameArray("d", new[] { "e", "f" });

            _writer.WriteObjectStart("g");
            _writer.WriteNameValue("h", "i");
            _writer.WriteObjectEnd();

            var expectedJson = new JObject();

            expectedJson["a"] = 1;
            expectedJson["B"] = "C";
            expectedJson["d"] = new JArray("e", "f");
            expectedJson["g"] = new JObject();
            expectedJson["g"]["h"] = "i";

            var actualJson = _writer.GetJObject();

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void GetJObject_MakesWriterReadOnly()
        {
            _writer.GetJObject();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue("a", 1));
        }

        [Fact]
        public void WriteObjectStart_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteObjectStart(name: null));
        }

        [Fact]
        public void WriteObjectStart_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectStart("a"));
        }

        [Fact]
        public void WriteObjectStart_SupportsEmptyName()
        {
            _writer.WriteObjectStart(name: "");
            _writer.WriteObjectEnd();

            const string expectedJson = @"{
  """": {}
}";
            var actualJson = _writer.GetJson();

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void WriteObjectEnd_ThrowsIfReadOnly()
        {
            _writer.WriteObjectStart("a");

            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectEnd());
        }

        [Fact]
        public void WriteObjectEnd_ThrowsIfCalledOnRoot()
        {
            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectEnd());
        }

        [Fact]
        public void WriteNameValue_WithIntValue_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameValue(name: null, value: 0));
        }

        [Fact]
        public void WriteNameValue_WithIntValue_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue("a", 1));
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameValue(name: null, value: true));
        }

        [Fact]
        public void WriteNameValue_WithBoolValue_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue("a", true));
        }

        [Fact]
        public void WriteArrayStart_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteArrayStart("a"));
        }

        [Fact]
        public void WriteArrayEnd_ThrowsIfReadOnly()
        {
            _writer.WriteArrayStart("a");

            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteArrayEnd());
        }

        [Fact]
        public void WriteObjectStartParameterless_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteObjectInArrayStart());
        }

        [Fact]
        public void WriteNameValue_WithIntValue_SupportsEmptyName()
        {
            _writer.WriteNameValue(name: "", value: 3);

            const string expectedJson = @"{
  """": 3
}";
            var actualJson = _writer.GetJson();

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void WriteNameValue_WithStringValue_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameValue(name: null, value: "a"));
        }

        [Fact]
        public void WriteNameValue_WithStringValue_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameValue("a", "b"));
        }

        [Fact]
        public void WriteNameValue_WithStringValue_SupportsEmptyNameAndEmptyValue()
        {
            _writer.WriteNameValue(name: "", value: "");

            const string expectedJson = @"{
  """": """"
}";
            var actualJson = _writer.GetJson();

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void WriteNameArray_ThrowsForNullName()
        {
            Assert.Throws<ArgumentNullException>(() => _writer.WriteNameArray(name: null, values: new[] { "b", "c" }));
        }

        [Fact]
        public void WriteNameArray_ThrowsIfReadOnly()
        {
            MakeReadOnly();

            Assert.Throws<InvalidOperationException>(() => _writer.WriteNameArray("a", new[] { "b", "c" }));
        }

        [Fact]
        public void WriteNameArray_SupportsEmptyNameAndEmptyValues()
        {
            _writer.WriteNameArray(name: "", values: Enumerable.Empty<string>());

            const string expectedJson = @"{
  """": []
}";
            var actualJson = _writer.GetJson();

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void WriteNewArray_SupportsEmptyNameAndEmptyValues()
        {
            _writer.WriteArrayStart("");
            _writer.WriteArrayEnd();

            const string expectedJson = @"{
  """": []
}";
            var actualJson = _writer.GetJson();

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void WriteNewArray_CanWriteSimpleObjects()
        {
            _writer.WriteArrayStart("a");

            _writer.WriteObjectInArrayStart();
            _writer.WriteNameValue("b", "");
            _writer.WriteObjectEnd();

            _writer.WriteObjectInArrayStart();
            _writer.WriteNameValue("c", "");
            _writer.WriteObjectEnd();

            _writer.WriteArrayEnd();

            const string expectedJson = @"{
  ""a"": [
    {
      ""b"": """"
    },
    {
      ""c"": """"
    }
  ]
}";
            var actualJson = _writer.GetJson();

            Assert.Equal(expectedJson, actualJson);
        }


        private void MakeReadOnly()
        {
            _writer.GetJson();
        }
    }
}