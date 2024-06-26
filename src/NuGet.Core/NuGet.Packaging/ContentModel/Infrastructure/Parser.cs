// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace NuGet.ContentModel.Infrastructure
{
    public class PatternExpression
    {
        private readonly List<Segment> _segments = new List<Segment>();
        private readonly Dictionary<string, object> _defaults;
        private readonly PatternTable _table;

        public PatternExpression(PatternDefinition pattern)
        {
            _table = pattern.Table;
            _defaults = pattern.Defaults.ToDictionary(p => p.Key, p => p.Value);
            Initialize(pattern.Pattern);
        }

        private void Initialize(List<string> pattern)
        {
            foreach (var node in pattern)
            {
                if (node.StartsWith("{") && node.EndsWith("}"))
                {
                    // Extract the token name from the pattern, assuming format "{token}" or "{token?}"
                    var matchOnly = node.EndsWith("?}");
                    var tokenName = node.Substring(1, node.Length - (matchOnly ? 3 : 2));

                    _segments.Add(new TokenSegment(tokenName, matchOnly, _table));
                }
                else
                {
                    // Treat the entire string as a literal segment
                    _segments.Add(new LiteralSegment(node));
                }
            }
        }

        internal ContentItem Match(List<string> path, IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions)
        {
            ContentItem item = null;
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var pathNode = path[i];
                if (segment.TryMatch(ref item, pathNode, propertyDefinitions))
                {
                    continue;
                }
                return null;
            }

            // Successful match!
            // Apply defaults from the pattern
            if (item == null)
            {
                // item not created, use shared defaults
                item = new ContentItem
                {
                    Path = string.Join("/", path),
                    Properties = _defaults
                };
            }
            else
            {
                // item already created, append defaults
                foreach (var pair in _defaults)
                {
                    item.Properties[pair.Key] = pair.Value;
                }
            }

            return item;
        }

        private abstract class Segment
        {
            internal abstract bool TryMatch(ref ContentItem item, string segment, IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions);
        }

        [DebuggerDisplay("{_pattern.Substring(_start, _length)}")]
        private class LiteralSegment : Segment
        {
            private readonly string _patternNode;

            public LiteralSegment(string patternNode)
            {
                _patternNode = patternNode;
            }

            internal override bool TryMatch(
                ref ContentItem item,
                string segment,
                IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions)
            {
                if (string.Compare(segment, _patternNode, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                return false;
            }
        }

        [DebuggerDisplay("Token = {_token}, Delimiter = {_delimiter}, MatchOnly = {_matchOnly}")]
        private class TokenSegment : Segment
        {
            private readonly string _token;
            private readonly bool _matchOnly;
            private readonly PatternTable _table;

            public TokenSegment(string token, bool matchOnly, PatternTable table)
            {
                _token = token;
                _matchOnly = matchOnly;
                _table = table;
            }

            internal override bool TryMatch(
                ref ContentItem item,
                string segment,
                IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions)
            {
                ContentPropertyDefinition propertyDefinition;
                if (!propertyDefinitions.TryGetValue(_token, out propertyDefinition))
                {
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, "Unable to find property definition for {{{0}}}", _token));
                }

                object value;
                if (propertyDefinition.TryLookup(segment, _table, out value))
                {
                    if (!_matchOnly)
                    {
                        // Adding property, create item if not already created
                        if (item == null)
                        {
                            item = new ContentItem
                            {
                                Path = segment
                            };
                        }

                        if (StringComparer.Ordinal.Equals(_token, "tfm"))
                        {
                            item.Properties.Add("tfm_raw", segment);
                        }
                        item.Properties.Add(_token, value);
                    }

                    return true;
                }
                return false;
            }
        }
    }
}
