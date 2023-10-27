// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal class Table
    {
        private readonly List<string[]> _rows = new List<string[]>();
        private int[] _columnWidths;
        private int[] _columnsToHighlight;

        public Table(int[] columnsToHighlight, params string[] headers)
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

        public void PrintResult(string searchTerm, ILogger logger)
        {
            if (_rows.Count <= 1)
            {
                logger.LogMinimal("No results found.");
                return;
            }

            foreach (var row in _rows)
            {
                string line = "";

                for (int i = 0; i < row.Length; i++)
                {
                    var paddedValue = (row[i] ?? string.Empty).PadRight(_columnWidths[i]);
                    line += "| " + paddedValue + " ";
                }

                line += "|";
                logger.LogMinimal(line);

                if (row == _rows.First())
                {
                    line = "";

                    foreach (var width in _columnWidths)
                    {
                        line += "|" + new string('-', width + 2);
                    }

                    line += "|";
                    logger.LogMinimal(line);
                }
            }
        }
    }
}
