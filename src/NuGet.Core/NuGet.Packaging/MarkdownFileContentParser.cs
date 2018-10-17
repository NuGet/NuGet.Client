// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;

namespace NuGet.Packaging
{
    public static class MarkdownFileContentParser
    {
#pragma warning disable CS3002 // Return type is not CLS-compliant
        public static MarkdownPipeline CreateMarkdownPipeline(bool useDefaultParsers = false, bool disableHtml)
#pragma warning restore CS3002 // Return type is not CLS-compliant
        {
            var builder = new MarkdownPipelineBuilder();

            if (!useDefaultParsers)
            {
                // Clear all the default parsers
                builder.BlockParsers.Clear();
                builder.InlineParsers.Clear();

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

                builder.InlineParsers.AddRange(new InlineParser[]
                {
                new HtmlEntityParser(),
                new LinkInlineParser(),
                new EscapeInlineParser(),
                new EmphasisInlineParser(),
                new CodeInlineParser(),
                new AutolineInlineParser(),
                new LineBreakInlineParser(),
                });
            }

            if (disableHtml)
            {
                builder.DisableHtml();
            }
            return builder.Build();
        }
    }
}
