// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class MinPublishAgeExceptionItem : SettingItem
    {
        private string _pattern = string.Empty;

        public override string ElementName => "add";

        public required string Pattern
        {
            get => _pattern;
            init
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Empty_Or_WhiteSpaceOnly, nameof(value));
                }

                if (value.Length > PackageSourceMapping.PackageIdMaxLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                AddOrUpdateAttribute(ConfigurationConstants.PatternAttribute, value);
                _pattern = value;
            }
        }

        protected override IReadOnlyCollection<string> RequiredAttributes { get; } =
            new HashSet<string>(new[] { ConfigurationConstants.PatternAttribute });

        public MinPublishAgeExceptionItem()
            : base()
        {
        }

        [SetsRequiredMembers]
        internal MinPublishAgeExceptionItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            Pattern = Attributes[ConfigurationConstants.PatternAttribute];
        }

        public override SettingBase Clone()
        {
            var newItem = new MinPublishAgeExceptionItem
            {
                Pattern = Pattern
            };

            if (Origin != null)
            {
                newItem.SetOrigin(Origin);
            }

            return newItem;
        }

        public override bool Equals(object? other)
        {
            if (other is MinPublishAgeExceptionItem item)
            {
                return string.Equals(Pattern, item.Pattern, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Pattern);
    }
}
