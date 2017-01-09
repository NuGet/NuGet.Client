// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text;

namespace NuGet.Versioning
{
    /// <summary>
    /// VersionRange formatter
    /// </summary>
    public class VersionRangeFormatter : IFormatProvider, ICustomFormatter
    {
        private const string LessThanOrEqualTo = "<=";
        private const string GreaterThanOrEqualTo = ">=";
        private const string ZeroN = "{0:N}";
        private readonly VersionFormatter _versionFormatter;

        /// <summary>
        /// Custom version range format provider.
        /// </summary>
        public VersionRangeFormatter()
        {
            _versionFormatter = VersionFormatter.Instance;
        }

        /// <summary>
        /// Format a version range string.
        /// </summary>
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }

            string formatted = null;
            var argType = arg.GetType();

            if (argType == typeof(IFormattable))
            {
                formatted = ((IFormattable)arg).ToString(format, formatProvider);
            }
            else if (!String.IsNullOrEmpty(format))
            {
                var range = arg as VersionRange;

                if (range != null)
                {
                    // single char identifiers
                    if (format.Length == 1)
                    {
                        formatted = Format(format[0], range);
                    }
                    else
                    {
                        var sb = new StringBuilder(format.Length);

                        for (var i = 0; i < format.Length; i++)
                        {
                            var s = Format(format[i], range);

                            if (s == null)
                            {
                                sb.Append(format[i]);
                            }
                            else
                            {
                                sb.Append(s);
                            }
                        }

                        formatted = sb.ToString();
                    }
                }
            }

            return formatted;
        }

        /// <summary>
        /// Format type.
        /// </summary>
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter)
                || formatType == typeof(VersionRange))
            {
                return this;
            }

            return null;
        }

        private string Format(char c, VersionRange range)
        {
            string s = null;

            switch (c)
            {
                case 'P':
                    s = PrettyPrint(range);
                    break;
                case 'L':
                    s = range.HasLowerBound ? string.Format(VersionFormatter.Instance, ZeroN, range.MinVersion) : string.Empty;
                    break;
                case 'U':
                    s = range.HasUpperBound ? string.Format(VersionFormatter.Instance, ZeroN, range.MaxVersion) : string.Empty;
                    break;
                case 'S':
                    s = GetToString(range);
                    break;
                case 'N':
                    s = GetNormalizedString(range);
                    break;
                case 'D':
                    s = GetLegacyString(range);
                    break;
                case 'T':
                    s = GetLegacyShortString(range);
                    break;
            }

            return s;
        }

        /// <summary>
        /// Builds a normalized string with no short hand
        /// </summary>
        private string GetNormalizedString(VersionRange range)
        {
            // TODO: write out the float version
            var sb = new StringBuilder();

            sb.Append(range.HasLowerBound && range.IsMinInclusive ? '[' : '(');

            if (range.HasLowerBound)
            {
                if (range.IsFloating)
                {
                    sb.Append(range.Float.ToString());
                }
                else
                {
                    sb.AppendFormat(_versionFormatter, ZeroN, range.MinVersion);
                }
            }

            sb.Append(", ");

            if (range.HasUpperBound)
            {
                sb.AppendFormat(_versionFormatter, ZeroN, range.MaxVersion);
            }

            sb.Append(range.HasUpperBound && range.IsMaxInclusive ? ']' : ')');

            return sb.ToString();
        }

        /// <summary>
        /// Builds a string to represent the VersionRange. This string can include short hand notations.
        /// </summary>
        private string GetToString(VersionRange range)
        {
            string s = null;

            if (range.HasLowerBound
                && range.IsMinInclusive
                && !range.HasUpperBound)
            {
                s = string.Format(_versionFormatter, ZeroN, range.MinVersion);
            }
            else if (range.HasLowerAndUpperBounds
                     && range.IsMinInclusive
                     && range.IsMaxInclusive
                     &&
                     range.MinVersion.Equals(range.MaxVersion))
            {
                // TODO: Does this need a specific version comparision? Does metadata matter?

                s = string.Format(_versionFormatter, "[{0:N}]", range.MinVersion);
            }
            else
            {
                s = GetNormalizedString(range);
            }

            return s;
        }

        /// <summary>
        /// Creates a legacy short string that is compatible with NuGet 2.8.3
        /// </summary>
        private string GetLegacyShortString(VersionRangeBase range)
        {
            string s = null;

            if (range.HasLowerBound
                && range.IsMinInclusive
                && !range.HasUpperBound)
            {
                s = string.Format(_versionFormatter, ZeroN, range.MinVersion);
            }
            else if (range.HasLowerAndUpperBounds
                     && range.IsMinInclusive
                     && range.IsMaxInclusive
                     &&
                     range.MinVersion.Equals(range.MaxVersion))
            {
                s = string.Format(_versionFormatter, "[{0:N}]", range.MinVersion);
            }
            else
            {
                s = GetLegacyString(range);
            }

            return s;
        }

        /// <summary>
        /// Creates a legacy string that is compatible with NuGet 2.8.3
        /// </summary>
        private string GetLegacyString(VersionRangeBase range)
        {
            var sb = new StringBuilder();

            sb.Append(range.HasLowerBound && range.IsMinInclusive ? '[' : '(');

            if (range.HasLowerBound)
            {
                sb.AppendFormat(_versionFormatter, ZeroN, range.MinVersion);
            }

            sb.Append(", ");

            if (range.HasUpperBound)
            {
                sb.AppendFormat(_versionFormatter, ZeroN, range.MaxVersion);
            }

            sb.Append(range.HasUpperBound && range.IsMaxInclusive ? ']' : ')');

            return sb.ToString();
        }

        /// <summary>
        /// A pretty print representation of the VersionRange.
        /// </summary>
        private string PrettyPrint(VersionRange range)
        {
            var sb = new StringBuilder("(");

            // no upper
            if (range.HasLowerBound
                && !range.HasUpperBound)
            {
                sb.Append(GreaterThanOrEqualTo);
                sb.AppendFormat(_versionFormatter, " {0:N}", range.MinVersion);
            }
            // single version
            else if (range.HasLowerAndUpperBounds
                     && range.MaxVersion.Equals(range.MinVersion)
                     && range.IsMinInclusive
                     && range.IsMaxInclusive)
            {
                sb.AppendFormat(_versionFormatter, "= {0:N}", range.MinVersion);
            }
            else // normal range
            {
                if (range.HasLowerBound)
                {
                    if (range.IsMinInclusive)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} ", GreaterThanOrEqualTo);
                    }
                    else
                    {
                        sb.Append("> ");
                    }

                    sb.AppendFormat(_versionFormatter, ZeroN, range.MinVersion);
                }

                if (range.HasLowerAndUpperBounds)
                {
                    sb.Append(" && ");
                }

                if (range.HasUpperBound)
                {
                    if (range.IsMaxInclusive)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} ", LessThanOrEqualTo);
                    }
                    else
                    {
                        sb.Append("< ");
                    }

                    sb.AppendFormat(_versionFormatter, ZeroN, range.MaxVersion);
                }
            }

            sb.Append(")");

            // avoid ()
            if (sb.Length == 2)
            {
                sb.Clear();
            }

            return sb.ToString();
        }
    }
}
