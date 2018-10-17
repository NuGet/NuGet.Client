// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;

namespace NuGet.Packaging.Licenses
{
    public class LicenseContentParser
    {
        public static string ToSafeHtmlString(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            var builder = new MarkdownPipelineBuilder();

            builder.BlockParsers.Clear();
            builder.BlockParsers.AddRange(new BlockParser[]
            {
                new ThematicBreakParser(),
                new HeadingBlockParser(),
                new QuoteBlockParser(),
                new ListBlockParser(),

                new HtmlBlockParser(),
                new FencedCodeBlockParser(),
                new IndentedCodeBlockParser(),
                new ParagraphBlockParser(),
            });

            builder.InlineParsers.Clear();
            builder.InlineParsers.AddRange(new InlineParser[]
            {
                new HtmlEntityParser(),
                //new LinkInlineParser(),
                new EscapeInlineParser(),
                new EmphasisInlineParser(),
                new CodeInlineParser(),
                new AutolineInlineParser(),
                new LineBreakInlineParser(),
            });

            builder.DisableHtml();
            return Markdown.ToHtml(content, builder.Build());
        }
    }
}
