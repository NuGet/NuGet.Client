// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="JsonConverter{T}"/> to allow System.Text.Json to read/write <see cref="AssetsLogMessage"/> where the list is setup as an object
    /// </summary>
    internal class AssetsLogMessageConverter : JsonConverter<AssetsLogMessage>
    {
        private static readonly byte[] Utf8Level = Encoding.UTF8.GetBytes(LogMessageProperties.LEVEL);
        private static readonly byte[] Utf8Code = Encoding.UTF8.GetBytes(LogMessageProperties.CODE);
        private static readonly byte[] Utf8WarningLevel = Encoding.UTF8.GetBytes(LogMessageProperties.WARNING_LEVEL);
        private static readonly byte[] Utf8FilePath = Encoding.UTF8.GetBytes(LogMessageProperties.FILE_PATH);
        private static readonly byte[] Utf8StartLineNumber = Encoding.UTF8.GetBytes(LogMessageProperties.START_LINE_NUMBER);
        private static readonly byte[] Utf8StartColumnNumber = Encoding.UTF8.GetBytes(LogMessageProperties.START_COLUMN_NUMBER);
        private static readonly byte[] Utf8EndLineNumber = Encoding.UTF8.GetBytes(LogMessageProperties.END_LINE_NUMBER);
        private static readonly byte[] Utf8EndColumnNumber = Encoding.UTF8.GetBytes(LogMessageProperties.END_COLUMN_NUMBER);
        private static readonly byte[] Utf8Message = Encoding.UTF8.GetBytes(LogMessageProperties.MESSAGE);
        private static readonly byte[] Utf8LibraryId = Encoding.UTF8.GetBytes(LogMessageProperties.LIBRARY_ID);
        private static readonly byte[] Utf8TargetGraphs = Encoding.UTF8.GetBytes(LogMessageProperties.TARGET_GRAPHS);

        public override AssetsLogMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            if (typeToConvert != typeof(AssetsLogMessage))
            {
                throw new InvalidOperationException();
            }

            var isValid = true;
            LogLevel level = default;
            NuGetLogCode code = default;
            //matching default warning level when AssetLogMessage object is created
            WarningLevel warningLevel = WarningLevel.Severe;
            string message = default;
            string filePath = default;
            int startLineNumber = default;
            int startColNumber = default;
            int endLineNumber = default;
            int endColNumber = default;
            string libraryId = default;
            IReadOnlyList<string> targetGraphs = null;

            while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (!isValid)
                {
                    reader.Skip();
                }
                if (reader.ValueTextEquals(Utf8Level))
                {
                    var levelString = reader.ReadNextTokenAsString();
                    isValid &= Enum.TryParse(levelString, out level);
                }
                else if (reader.ValueTextEquals(Utf8Code))
                {
                    var codeString = reader.ReadNextTokenAsString();
                    isValid &= Enum.TryParse(codeString, out code);
                }
                else if (reader.ValueTextEquals(Utf8WarningLevel))
                {
                    reader.ReadNextToken();
                    warningLevel = (WarningLevel)Enum.ToObject(typeof(WarningLevel), reader.GetInt32());
                }
                else if (reader.ValueTextEquals(Utf8FilePath))
                {
                    filePath = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8StartLineNumber))
                {
                    reader.ReadNextToken();
                    startLineNumber = reader.GetInt32();
                }
                else if (reader.ValueTextEquals(Utf8StartColumnNumber))
                {
                    reader.ReadNextToken();
                    startColNumber = reader.GetInt32();
                }
                else if (reader.ValueTextEquals(Utf8EndLineNumber))
                {
                    reader.ReadNextToken();
                    endLineNumber = reader.GetInt32();
                }
                else if (reader.ValueTextEquals(Utf8EndColumnNumber))
                {
                    reader.ReadNextToken();
                    endColNumber = reader.GetInt32();
                }
                else if (reader.ValueTextEquals(Utf8Message))
                {
                    message = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8LibraryId))
                {
                    libraryId = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8TargetGraphs))
                {
                    targetGraphs = reader.ReadNextStringArrayAsList();
                }
                else
                {
                    reader.Skip();
                }
            }
            if (isValid)
            {
                var assetLogMessage = new AssetsLogMessage(level, code, message)
                {
                    TargetGraphs = targetGraphs ?? new List<string>(0),
                    FilePath = filePath,
                    EndColumnNumber = endColNumber,
                    EndLineNumber = endLineNumber,
                    LibraryId = libraryId,
                    StartColumnNumber = startColNumber,
                    StartLineNumber = startLineNumber,
                    WarningLevel = warningLevel
                };
                return assetLogMessage;
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, AssetsLogMessage value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
