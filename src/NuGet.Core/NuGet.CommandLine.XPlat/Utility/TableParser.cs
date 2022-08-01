// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NuGet.CommandLine.XPlat.ReportRenderers;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal static class TableParser
    {
        internal static (IEnumerable<FormattedCell>, ReportFrameworkPackage) ToStringTable<T>(
          this IEnumerable<T> values,
          string[] columnHeaders,
          string framework,
          bool printingTransitive,
          ReportOutputFormat reportOutputFormat,
          Func<T, FormattedCell>[] valueSelectors,
          List<Func<T, IEnumerable<FormattedCell>>> vulnerabilityValueSelectors)
        {
            return ToFormattedStringTable(values.ToArray(), columnHeaders, framework, printingTransitive, reportOutputFormat, valueSelectors, vulnerabilityValueSelectors);
        }

        internal static (IEnumerable<FormattedCell>, ReportFrameworkPackage) ToFormattedStringTable<T>(
          this T[] values,
          string[] columnHeaders,
          string framework,
          bool printingTransitive,
          ReportOutputFormat reportOutputFormat,
          Func<T, FormattedCell>[] valueSelectors,
          List<Func<T, IEnumerable<FormattedCell>>> vulnerabilityValueSelectors)
        {
            var stringTable = new List<List<FormattedCell>>();

            // Fill headers
            if (columnHeaders != null)
            {
                Debug.Assert(columnHeaders.Length == valueSelectors.Length + (vulnerabilityValueSelectors == null ? 0 : vulnerabilityValueSelectors.Count));

                var headers = new List<FormattedCell>();
                headers.AddRange(columnHeaders.Select(h => new FormattedCell(h, printingTransitive ? ReportPackageColumn.TransitivePackage : ReportPackageColumn.TopLevelPackage)));
                stringTable.Add(headers);
            }

            List<TopLevelPackage> topLevelPackages = new();
            List<TransitivePackage> transitivePackages = printingTransitive ? new() : null;

            // Fill table rows - we need a queue for multi-line values
            var columnQueues = new Dictionary<int, Queue<FormattedCell>>();
            for (var rowIndex = 0; rowIndex < values.Length; rowIndex++)
            {
                // process row
                var row = new List<FormattedCell>();
                for (var colIndex = 0; colIndex < valueSelectors.Length; colIndex++)
                {
                    FormattedCell formattedDataCell = valueSelectors[colIndex](values[rowIndex]);
                    // the normal case
                    formattedDataCell.Value = (reportOutputFormat == ReportOutputFormat.Console && colIndex == 0 ? "> " : "") + formattedDataCell.Value?.ToString() ?? string.Empty;
                    row.Add(formattedDataCell);
                }

                for (var colIndex = 0; colIndex < vulnerabilityValueSelectors?.Count; colIndex++)
                {
                    IEnumerable<FormattedCell> dataEnum = vulnerabilityValueSelectors[colIndex](values[rowIndex]);
                    // we have a potential multi-line value--we need to add the first line and store remainder
                    var firstLine = true;
                    var queue = new Queue<FormattedCell>();
                    foreach (FormattedCell formattedDataCell in dataEnum)
                    {
                        formattedDataCell.Value = formattedDataCell.Value?.ToString() ?? string.Empty;
                        if (firstLine)
                        {
                            // print it
                            row.Add(formattedDataCell);
                            firstLine = false;
                        }
                        else
                        {
                            // store the rest
                            queue.Enqueue(formattedDataCell);
                        }
                    }

                    if (queue.Count > 0) // only add a queue when there's something to store
                    {
                        columnQueues[valueSelectors.Length + colIndex] = queue;
                    }
                }

                stringTable.Add(row);

                if (printingTransitive)
                {
                    TransitivePackage transitivePackage = new();
                    foreach (FormattedCell formattedCell in row)
                    {
                        switch (formattedCell.ReportPackageColumn)
                        {
                            case ReportPackageColumn.EmptyColumn:
                                break;
                            case ReportPackageColumn.Requested:
                                break;
                            case ReportPackageColumn.Resolved:
                                transitivePackage.ResolvedVersion = formattedCell.Value;
                                break;
                            case ReportPackageColumn.TransitivePackage:
                                transitivePackage.PackageId = formattedCell.Value;
                                break;
                            case ReportPackageColumn.Latest:
                                transitivePackage.LatestVersion = formattedCell.Value;
                                break;
                            case ReportPackageColumn.Deprecated:
                                transitivePackage.DeprecationReasons = formattedCell.Value;
                                break;
                            case ReportPackageColumn.AlternatePackage:
                                break;
                            case ReportPackageColumn.Vulnerabilities:
                                break;
                            case ReportPackageColumn.VulnerabilitySeverity:
                                break;
                            case ReportPackageColumn.VulnerabilityAdvisoryurl:
                                break;
                            default:
                                break;
                        }
                    }

                    Debug.Assert(transitivePackage.PackageId != null);
                    transitivePackages.Add(transitivePackage);
                }
                else
                {
                    TopLevelPackage topLevelPackage = new();

                    foreach (FormattedCell formattedCell in row)
                    {
                        switch (formattedCell.ReportPackageColumn)
                        {
                            case ReportPackageColumn.TopLevelPackage:
                                topLevelPackage.PackageId = formattedCell.Value;
                                break;
                            case ReportPackageColumn.EmptyColumn:
                                break;
                            case ReportPackageColumn.Requested:
                                topLevelPackage.RequestedVersion = formattedCell.Value;
                                break;
                            case ReportPackageColumn.Resolved:
                                topLevelPackage.ResolvedVersion = formattedCell.Value;
                                break;
                            case ReportPackageColumn.Latest:
                                break;
                            case ReportPackageColumn.Deprecated:
                                break;
                            case ReportPackageColumn.AlternatePackage:
                                break;
                            case ReportPackageColumn.Vulnerabilities:
                                break;
                            case ReportPackageColumn.VulnerabilitySeverity:
                                break;
                            case ReportPackageColumn.VulnerabilityAdvisoryurl:
                                break;
                            default:
                                break;
                        }
                    }

                    Debug.Assert(topLevelPackage.PackageId != null);
                    topLevelPackages.Add(topLevelPackage);
                }

                // clear column queues (strings for subsequent rows for this value) before proceeding with next row
                while (columnQueues.Count > 0)
                {
                    var subsequentRow = new List<FormattedCell>();
                    for (var colIndex = 0; colIndex < valueSelectors.Length + vulnerabilityValueSelectors?.Count; colIndex++)
                    {
                        FormattedCell formattedDataCell = null;
                        if (columnQueues.TryGetValue(colIndex, out Queue<FormattedCell> thisColumnQueue)) // we have at least one remaining value for this column
                        {
                            formattedDataCell = thisColumnQueue.Dequeue();
                            if (thisColumnQueue.Count == 0)
                            {
                                columnQueues.Remove(colIndex); // once these are all cleared the outer loop will break
                            }
                        }
                        else
                        {
                            formattedDataCell = new FormattedCell(string.Empty, ReportPackageColumn.EmptyColumn);
                        }

                        subsequentRow.Add(formattedDataCell);
                    }

                    stringTable.Add(subsequentRow);
                }
            }

            return (ToPaddedStringTable(stringTable), new ReportFrameworkPackage(framework, topLevelPackages, transitivePackages));
        }

        internal static IEnumerable<FormattedCell> ToPaddedStringTable(IEnumerable<ICollection<FormattedCell>> values)
        {
            var maxColumnsWidth = GetMaxColumnsWidth(values);
            var stringTable = new List<FormattedCell>();

            foreach (ICollection<FormattedCell> row in values)
            {
                int colIndex = 0;
                foreach (var dataCell in row)
                {
                    dataCell.Value = "   " + dataCell.Value?.PadRight(maxColumnsWidth[colIndex]) ?? string.Empty;
                    stringTable.Add(dataCell);
                    colIndex++;
                }

                stringTable.Add(new FormattedCell(Environment.NewLine, ReportPackageColumn.EmptyColumn));
            }

            return stringTable;
        }

        private static int[] GetMaxColumnsWidth(IEnumerable<ICollection<FormattedCell>> values)
        {
            int[] maxColumnsWidth = null;
            foreach (var row in values)
            {
                // use the first row to get dimension
                if (maxColumnsWidth == null)
                {
                    maxColumnsWidth = new int[row.Count];
                }

                var colIndex = 0;
                foreach (var formattedDataCell in row)
                {
                    if ((formattedDataCell.Value?.Length ?? 0) > maxColumnsWidth[colIndex])
                    {
                        maxColumnsWidth[colIndex] = formattedDataCell.Value.Length;
                    }

                    colIndex++;
                }
            }

            return maxColumnsWidth;
        }
    }
}
