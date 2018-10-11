// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.PackageManagement.UI
{
    public class PackageLicenseInfo
    {
        public PackageLicenseInfo(
            string id,
            IList<IText> license,
            string authors)
        {
            Id = id;
            License = license;
            Authors = authors;
        }

        public string Id { get; }

        public IList<IText> License { get; }

        public string Authors { get; }
    }

    public class LicenseText : IText
    {
        public LicenseText(string text, Uri link)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Link = link ?? throw new ArgumentNullException(nameof(link));
        }

        public string Text { get; set; }
        public Uri Link { get; set; }
    }

    public class FreeText : IText
    {
        public FreeText(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    public class WarningText : IText
    {
        public WarningText(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }


    public interface IText
    {
        string Text { get; }
    }

}
