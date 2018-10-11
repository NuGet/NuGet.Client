// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;

namespace NuGet.PackageManagement.UI
{
    public interface IText
    {
        string Text { get; }
    }

    internal class LicenseText : IText
    {
        public LicenseText(string text, Uri link)
        {
            Text = text;
            Link = link;
        }

        public string Text { get; set; }
        public Uri Link { get; set; }
    }

    internal class FreeText : IText
    {
        public FreeText(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    internal class WarningText : IText
    {
        public WarningText(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }
}
