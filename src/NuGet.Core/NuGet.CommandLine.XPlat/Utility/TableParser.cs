using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NuGet.CommandLine.XPlat.Utility
{
    public static class TableParser
    {
        public static string ToStringTable<T>(
          this IEnumerable<T> values,
          string[] columnHeaders,
          params Func<T, object>[] valueSelectors)
        {
            return ToStringTable(values.ToArray(), columnHeaders, valueSelectors);
        }

        public static string ToStringTable<T>(
          this T[] values,
          string[] columnHeaders,
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

            return ToStringTable(arrValues);
        }

        public static string ToStringTable(this string[,] arrValues)
        {
            var maxColumnsWidth = GetMaxColumnsWidth(arrValues);

            var sb = new StringBuilder();
            for (var rowIndex = 0; rowIndex < arrValues.GetLength(0); rowIndex++)
            {
                for (var colIndex = 0; colIndex < arrValues.GetLength(1); colIndex++)
                {
                    // Prvar cell
                    var cell = arrValues[rowIndex, colIndex];
                    cell = cell.PadRight(maxColumnsWidth[colIndex]);
                    if (colIndex != 0)
                    {
                        sb.Append("     ");
                    }
                    sb.Append(cell);
                }

                // Prvar end of line
                sb.AppendLine();

            }

            return sb.ToString();
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
