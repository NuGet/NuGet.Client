// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.IO;
using System.Text;

namespace NuGet;

internal class NoAllocNewLineStreamWriter : StreamWriter
{
    public NoAllocNewLineStreamWriter(Stream stream, Encoding encoding, int bufferSize, bool leaveOpen) :
        base(stream, encoding, bufferSize, leaveOpen)
    {

    }

    public NoAllocNewLineStreamWriter(Stream stream) :
        base(stream)
    {

    }

    private string? _newLine;
    public override string NewLine
    {
        get
        {
            if (_newLine == null)
            {
                _newLine = new string(CoreNewLine);
            }

            return _newLine;
        }
        set
        {
            if (value == null)
            {
                value = "\r\n";
            }

            CoreNewLine = value.ToCharArray();
            _newLine = value;
        }
    }
}
