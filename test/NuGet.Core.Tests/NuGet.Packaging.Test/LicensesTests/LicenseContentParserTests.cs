// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root flicense information.

using NuGet.Packaging.Licenses;
using Xunit;

namespace NuGet.Packaging.Test.LicensesTests
{
    public class LicenseContentParserTests
    {
        [Theory]
        //[InlineData(@"> hello <a name=""n""
        //> href=""javascript:alert('xss')"" > *you*</a>",
        //"<blockquote>\n<p>hello &lt;a name=&quot;n&quot;\nhref=&quot;javascript:alert('xss')&quot; &gt; <em>you</em>&lt;/a&gt;</p>\n</blockquote>\n")]
        [InlineData(@"> hello <a name=""n""
> href=""javascript:alert('xss')"" > *you*</a>",
        "<blockquote>\n<p>hello &lt;a name=&quot;n&quot;\nhref=&quot;javascript:alert('xss')&quot; &gt; <em>you</em>&lt;/a&gt;</p>\n</blockquote>\n")]
        [InlineData(@"**bland**", "<p><strong>bland</strong></p>\n")]
        [InlineData(@"[yay](#outside)", "<p><a href=\"#outside\">yay</a></p>\n")]
        // check how links work. Maybe we should disable link parsing.
        // understand every single capability that's parsed.
        public void LicenseContentParser_ParseBasicContent(string given, string expected)
        {
            Assert.Equal(expected, LicenseContentParser.ToSafeHtmlString(given));
        }
    }
}
