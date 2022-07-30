// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Shared;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal class FormattedCell : IEquatable<FormattedCell>
    {
        public ReportPackageColumn ReportPackageColumn { get; set; }
        public string Value { get; set; }
        public ConsoleColor? ForegroundColor { get; set; }

        private FormattedCell()
        {

        }
        //public FormattedCell() : this(string.Empty) { }

        public FormattedCell(string value, ReportPackageColumn reportPackageColumn, ConsoleColor? foregroundColor = null)
        {
            Value = value ?? string.Empty;
            ReportPackageColumn = reportPackageColumn;
            ForegroundColor = foregroundColor;
        }

        public bool Equals(FormattedCell other) => Value == other?.Value && ForegroundColor == other?.ForegroundColor;

        public override bool Equals(object obj) => Equals(obj as FormattedCell);

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(StringComparer.Ordinal.GetHashCode(Value));
            combiner.AddStruct(ForegroundColor);

            return combiner.CombinedHash;
        }
    }
}
