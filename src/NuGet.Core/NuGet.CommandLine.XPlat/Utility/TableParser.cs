// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NuGet.CommandLine.XPlat.Utility
{
    public static class TableParser
    {
        internal static IEnumerable<Tuple<string, ConsoleColor>> ToStringTable<T>(
          this IEnumerable<T> values,
          string[] columnHeaders,
          Func<T, UpdateLevel> updateLevel,
          params Func<T, object>[] valueSelectors)
        {
            return ToStringTable(values.ToArray(), columnHeaders, updateLevel, valueSelectors);
        }

        internal static IEnumerable<Tuple<string, ConsoleColor>> ToStringTable<T>(
          this T[] values,
          string[] columnHeaders,
          Func<T, UpdateLevel> updateLevel,
          params Func<T, object>[] valueSelectors)
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
                    if (colIndex == 1)
                    {
                        arrValues[rowIndex, colIndex] = "> " + valueSelectors[colIndex]
                      .Invoke(values[rowIndex - headerSpace]).ToString();
                    }
                    else
                    {
                        arrValues[rowIndex, colIndex] = valueSelectors[colIndex]
                      .Invoke(values[rowIndex - headerSpace]).ToString();
                    }
                    
                }
            }

            return ToStringTable(values, arrValues, updateLevel);
        }

        internal static IEnumerable<Tuple<string, ConsoleColor>> ToStringTable<T>(this T[] values, string[,] arrValues, Func<T, UpdateLevel> updateLevel)
        {
            var maxColumnsWidth = GetMaxColumnsWidth(arrValues);
            var rows = new List<Tuple<string, ConsoleColor>>();

            for (var rowIndex = 0; rowIndex < arrValues.GetLength(0); rowIndex++)
            {
                for (var colIndex = 0; colIndex < arrValues.GetLength(1); colIndex++)
                {
                    if (colIndex == arrValues.GetLength(1) - 1 && rowIndex > 0)
                    {
                        var level = updateLevel.Invoke(values[rowIndex - 1]);
                        var cell = arrValues[rowIndex, colIndex];
                        cell = cell.PadRight(maxColumnsWidth[colIndex]);
                        if (colIndex != 0)
                        {
                            cell = "   " + cell;
                        }

                        if (level == UpdateLevel.Major)
                        {
                            rows.Add(Tuple.Create(cell, ConsoleColor.Red));
                        }
                        else if (level == UpdateLevel.Minor)
                        {
                            rows.Add(Tuple.Create(cell, ConsoleColor.Yellow));
                        }
                        else if (level == UpdateLevel.Patch)
                        {
                            rows.Add(Tuple.Create(cell, ConsoleColor.Green));
                        }
                        else
                        {
                            rows.Add(Tuple.Create(cell, ConsoleColor.White));
                        }
                    }
                    else
                    {
                        var cell = arrValues[rowIndex, colIndex];
                        cell = cell.PadRight(maxColumnsWidth[colIndex]);
                        if (colIndex != 0)
                        {
                            cell = "   " + cell;
                        }
                        rows.Add(Tuple.Create(cell, ConsoleColor.White));
                    }

                }
                rows.Add(Tuple.Create(Environment.NewLine, ConsoleColor.White));
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
