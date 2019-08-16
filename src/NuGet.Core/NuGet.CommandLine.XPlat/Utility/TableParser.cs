// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.CommandLine.XPlat.Utility
{
    public static class TableParser
    {
        internal static Task<IEnumerable<string>> ToStringTableAsync<T>(
          this IEnumerable<T> values,
          string[] columnHeaders,
          params Func<T, Task<object>>[] valueSelectors)
        {
            return ToStringTableAsync(values.ToArray(), columnHeaders, valueSelectors);
        }

        internal static async Task<IEnumerable<string>> ToStringTableAsync<T>(
          this T[] values,
          string[] columnHeaders,
          params Func<T, Task<object>>[] valueSelectors)
        {
            var headerSpace = 1;

            if (columnHeaders == null)
            {
                headerSpace = 0;
            }

            var arrValues = new string[values.Length + headerSpace, valueSelectors.Length];

            // Fill headers
            if (columnHeaders != null)
            {
                Debug.Assert(columnHeaders.Length == valueSelectors.Length);

                for (var colIndex = 0; colIndex < arrValues.GetLength(1); colIndex++)
                {
                    arrValues[0, colIndex] = columnHeaders[colIndex];
                }
            }

            // Fill table rows
            for (var rowIndex = headerSpace; rowIndex < arrValues.GetLength(0); rowIndex++)
            {
                for (var colIndex = 0; colIndex < arrValues.GetLength(1); colIndex++)
                {
                    var data = await valueSelectors[colIndex](values[rowIndex - headerSpace]);
                    var cellContent = (colIndex == 1 ? "> " : "") + data.ToString();
                    arrValues[rowIndex, colIndex] = cellContent;
                }
            }

            return ToStringTable(values, arrValues);
        }

        internal static IEnumerable<string> ToStringTable<T>(this T[] values, string[,] arrValues)
        {
            var maxColumnsWidth = GetMaxColumnsWidth(arrValues);
            var rows = new List<string>();

            for (var rowIndex = 0; rowIndex < arrValues.GetLength(0); rowIndex++)
            {
                for (var colIndex = 0; colIndex < arrValues.GetLength(1); colIndex++)
                {
                    var cell = arrValues[rowIndex, colIndex];
                    cell = cell.PadRight(maxColumnsWidth[colIndex]);

                    if (colIndex != 0)
                    {
                        cell = "   " + cell;
                    }

                    rows.Add(cell);
                }

                rows.Add(Environment.NewLine);
            }

            return rows;
        }

        private static int[] GetMaxColumnsWidth(string[,] arrValues)
        {
            var maxColumnsWidth = new int[arrValues.GetLength(1)];
            for (var colIndex = 0; colIndex < arrValues.GetLength(1); colIndex++)
            {
                for (var rowIndex = 0; rowIndex < arrValues.GetLength(0); rowIndex++)
                {
                    var newLength = arrValues[rowIndex, colIndex].Length;
                    var oldLength = maxColumnsWidth[colIndex];

                    if (newLength > oldLength)
                    {
                        maxColumnsWidth[colIndex] = newLength;
                    }
                }
            }

            return maxColumnsWidth;
        }
    }
}
