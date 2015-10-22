// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.ProjectModel
{
    public class FileFormatException : Exception
    {
        public FileFormatException(string message)
            : base(message)
        {
        }

        public FileFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public string Path { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        private FileFormatException WithFilePath(string path)
        {
            Path = path;

            return this;
        }
        private FileFormatException WithLineInfo(JsonReaderException exception)
        {
            Line = exception.LinePosition;
            Column = exception.LineNumber;

            return this;
        }

        private FileFormatException WithLineInfo(IJsonLineInfo lineInfo)
        {
            Line = lineInfo.LineNumber;
            Column = lineInfo.LinePosition;

            return this;
        }

        public static FileFormatException Create(Exception exception, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            var message = Strings.FormatLog_ErrorReadingProjectJsonWithLocation(
                path,
                lineInfo.LineNumber,
                lineInfo.LinePosition,
                exception.Message);

            var ex = new FileFormatException(message, exception);

            return ex.WithFilePath(path).WithLineInfo(lineInfo);
        }

        public static FileFormatException Create(string message, JToken value, string path)
        {
            var lineInfo = (IJsonLineInfo)value;

            var ex = new FileFormatException(message);

            return ex.WithFilePath(path).WithLineInfo(lineInfo);
        }

        internal static FileFormatException Create(Exception exception, string path)
        {
            var jex = exception as JsonReaderException;

            string message;
            if (jex == null)
            {
                message = Strings.FormatLog_ErrorReadingProjectJson(path, exception.Message);
                return new FileFormatException(message, exception).WithFilePath(path);
            }
            else
            {
                message = Strings.FormatLog_ErrorReadingProjectJsonWithLocation(path, jex.LineNumber, jex.LinePosition, exception.Message);
                return new FileFormatException(message, exception)
                    .WithFilePath(path)
                    .WithLineInfo(jex);
            }
        }
    }
}
