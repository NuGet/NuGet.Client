// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultTable
    {
        private readonly List<string[]> _rows = new List<string[]>();
        private int[] _columnWidths;
        private int[] _columnsToHighlight;

        public PackageSearchResultTable(int[] columnsToHighlight, params string[] headers)
        {
            _columnsToHighlight = columnsToHighlight;
            _columnWidths = new int[headers.Length];

            for (int i = 0; i < headers.Length; i++)
            {
                _columnWidths[i] = headers[i].Length;
            }

            _rows.Add(headers);
        }

        public void AddRow(params string[] row)
        {
            if (row.Length != _columnWidths.Length)
            {
                throw new InvalidOperationException("Row column count does not match header column count.");
            }

            for (int i = 0; i < row.Length; i++)
            {
                _columnWidths[i] = Math.Max(_columnWidths[i], row[i]?.Length ?? 0);
            }

            _rows.Add(row);
        }

        public void PrintResult(string searchTerm = null)
        {
            ConsoleColor highlighterColor = GetHighlighterColor();

            // If only headers are present (i.e., no package rows)
            if (_rows.Count <= 1)
            {
                Console.WriteLine("No results found.");
                return;
            }

            foreach (var row in _rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    var paddedValue = (row[i] ?? string.Empty).PadRight(_columnWidths[i]);

                    if (!string.IsNullOrEmpty(searchTerm) && paddedValue.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.Write("| ");
                        if (_columnsToHighlight.Contains(i)) PrintWithHighlight(paddedValue, searchTerm, highlighterColor);
                        else Console.Write(paddedValue);
                        Console.Write(" ");
                    }
                    else
                    {
                        Console.Write("| " + paddedValue + " ");
                    }
                }

                Console.WriteLine("|");

                if (row == _rows.First())
                {
                    // Add the separator after the header.
                    foreach (var width in _columnWidths)
                    {
                        Console.Write("|" + new string('-', width + 2));
                    }

                    Console.WriteLine("|");
                }
            }
        }

        private static void PrintWithHighlight(string value, string searchTerm, ConsoleColor highlighterColor)
        {
            int index = value.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
            ConsoleColor originalColor = Console.ForegroundColor;

            while (index != -1)
            {
                Console.Write(value.Substring(0, index));
                Console.ForegroundColor = highlighterColor;
                Console.Write(value.Substring(index, searchTerm.Length));
                Console.ForegroundColor = originalColor;
                value = value.Substring(index + searchTerm.Length);
                index = value.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
            }

            Console.Write(value);
        }

        private static ConsoleColor GetHighlighterColor()
        {
            if (Console.ForegroundColor == ConsoleColor.Red || Console.BackgroundColor == ConsoleColor.Red)
            {
                return ConsoleColor.Blue;
            }

            return ConsoleColor.Red;
        }
    }
}
