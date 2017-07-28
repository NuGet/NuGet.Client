// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.ContentModel.Infrastructure
{
    public class PatternExpression
    {
        private readonly List<Segment> _segments = new List<Segment>();
        private readonly IReadOnlyDictionary<string, object> _defaults;
        private readonly PatternTable _table;

        public PatternExpression(PatternDefinition pattern)
        {
            _table = pattern.Table;
            _defaults = pattern.Defaults;
            Initialize(pattern.Pattern);
        }

        private void Initialize(string pattern)
        {
            for (var scanIndex = 0; scanIndex < pattern.Length;)
            {
                var beginToken = pattern.Length;
                var endToken = pattern.Length;
                for (var i = scanIndex; i < pattern.Length; i++)
                {
                    var ch = pattern[i];
                    if (beginToken == pattern.Length)
                    {
                        if (ch == '{')
                        {
                            beginToken = i;
                        }
                    }
                    else if (ch == '}')
                    {
                        endToken = i;
                        break;
                    }
                }

                if (scanIndex != beginToken)
                {
                    var literal = pattern.Substring(scanIndex, beginToken - scanIndex);
                    _segments.Add(new LiteralSegment(literal));
                }
                if (beginToken != endToken)
                {
                    var delimiter = endToken + 1 < pattern.Length ? pattern[endToken + 1] : '\0';
                    var matchOnly = pattern[endToken - 1] == '?';

                    var beginName = beginToken + 1;
                    var endName = endToken - (matchOnly ? 1 : 0);

                    var tokenName = pattern.Substring(beginName, endName - beginName);
                    _segments.Add(new TokenSegment(tokenName, delimiter, matchOnly, _table));
                }
                scanIndex = endToken + 1;
            }
        }

        public ContentItem Match(string path, IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions)
        {
            var item = CreateContentItem(path);

            var startIndex = 0;
            foreach (var segment in _segments)
            {
                int endIndex;
                if (segment.TryMatch(item, propertyDefinitions, startIndex, out endIndex))
                {
                    startIndex = endIndex;
                    continue;
                }
                return DiscardContentItem(item);
            }

            if (startIndex == path.Length)
            {
                // Successful match!
                // Apply defaults from the pattern
                var defaults = _defaults as Dictionary<string, object>;
                if (defaults != null)
                {
                    // Use struct enum, to avoid boxing for Dictionary
                    foreach (var pair in defaults)
                    {
                        item.Properties[pair.Key] = pair.Value;
                    }
                }
                else
                {
                    foreach (var pair in _defaults)
                    {
                        item.Properties[pair.Key] = pair.Value;
                    }
                }
                return item;
            }
            return DiscardContentItem(item);
        }

        [ThreadStatic]
        private static ContentItem s_contentItemCache;

        private static ContentItem CreateContentItem(string path)
        {
            var contentItem = s_contentItemCache;
            if (contentItem == null)
            {
                return new ContentItem
                {
                    Path = path
                };
            }

            s_contentItemCache = null;
            contentItem.Path = path;
            return contentItem;
        }

        private static ContentItem DiscardContentItem(ContentItem item)
        {
            item.Path = string.Empty;
            item.Properties.Clear();
            s_contentItemCache = item;

            return null;
        }

        private abstract class Segment
        {
            internal abstract bool TryMatch(ContentItem item, IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions, int startIndex, out int endIndex);
        }

        private class LiteralSegment : Segment
        {
            private readonly string _literal;

            public LiteralSegment(string literal)
            {
                _literal = literal;
            }

            internal override bool TryMatch(
                ContentItem item,
                IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions,
                int startIndex,
                out int endIndex)
            {
                if (item.Path.Length >= startIndex + _literal.Length)
                {
                    var substring = item.Path.Substring(startIndex, _literal.Length);
                    if (string.Equals(_literal, substring, StringComparison.OrdinalIgnoreCase))
                    {
                        endIndex = startIndex + _literal.Length;
                        return true;
                    }
                }
                endIndex = startIndex;
                return false;
            }
        }

        private class TokenSegment : Segment
        {
            private readonly string _token;
            private readonly char _delimiter;
            private readonly bool _matchOnly;
            private readonly PatternTable _table;

            public TokenSegment(string token, char delimiter, bool matchOnly, PatternTable table)
            {
                _token = token;
                _delimiter = delimiter;
                _matchOnly = matchOnly;
                _table = table;
            }

            internal override bool TryMatch(ContentItem item, IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions, int startIndex, out int endIndex)
            {
                ContentPropertyDefinition propertyDefinition;
                if (!propertyDefinitions.TryGetValue(_token, out propertyDefinition))
                {
                    throw new Exception(string.Format("Unable to find property definition for {{{0}}}", _token));
                }
                var path = item.Path;

                for (var scanIndex = startIndex; scanIndex != path.Length;)
                {
                    var delimiterIndex = path.Length;
                    for (var i = scanIndex + 1; i < path.Length; i++)
                    {
                        if (path[i] == _delimiter)
                        {
                            delimiterIndex = i;
                            break;
                        }
                    }

                    if (delimiterIndex == path.Length
                        && _delimiter != '\0')
                    {
                        break;
                    }
                    var substring = path.Substring(startIndex, delimiterIndex - startIndex);
                    object value;
                    if (propertyDefinition.TryLookup(substring, _table, out value))
                    {
                        if (!_matchOnly)
                        {
                            item.Properties.Add(_token, value);
                        }
                        endIndex = delimiterIndex;
                        return true;
                    }
                    scanIndex = delimiterIndex;
                }
                endIndex = startIndex;
                return false;
            }
        }
    }
}
