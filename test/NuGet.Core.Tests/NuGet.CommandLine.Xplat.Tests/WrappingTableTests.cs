// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.CommandLine.XPlat;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class WrappingTableTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(50)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(800)]
        [InlineData(9)]
        public void AddRow_AddNColumnedRowInAnNColumnedTable_AddsRow(int columns)
        {
            // Arrange
            WrappingTable myTable = new WrappingTable(Array.Empty<int>(), Enumerable.Repeat("Header", columns).ToArray());
            List<List<string>> expectedTable =
            [
                Enumerable.Repeat("row 1", columns).ToList(),
            ];
            List<string> header = Enumerable.Repeat("Header", columns).ToList();

            // Act
            myTable.AddRow(Enumerable.Repeat("row 1", columns).ToList());

            // Assert
            Assert.Equal(expectedTable, myTable._rows);
            Assert.Equal(header, myTable._columns.Select(c => c.Header).ToList());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(400)]
        [InlineData(5)]
        [InlineData(60)]
        [InlineData(7)]
        public void AddRow_AddTwoColumnedRowInANotTwoColumnedTable_ThrowsAnException(int columns)
        {
            // Arrange
            WrappingTable myTable = new WrappingTable(Array.Empty<int>(), Enumerable.Repeat("Header", columns).ToArray());

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => myTable.AddRow(new List<string> { "row", "row column2" }));

            // Assert
            Assert.Equal("Row column count does not match header column count.", exception.Message);
        }

        [Fact]
        public void PrintResult_ZeroRowsAdded_PrintsNoResultFound()
        {
            // Arrange
            string searchTerm = "TestPackage";
            Mock<ILoggerWithColor> mockLoggerWithColor = new Mock<ILoggerWithColor>();
            WrappingTable table = new WrappingTable(Array.Empty<int>(), Enumerable.Repeat("header", 4).ToArray());

            // Act
            table.PrintWithHighlighting(mockLoggerWithColor.Object, searchTerm);

            // Assert
            mockLoggerWithColor.Verify(x => x.LogMinimal("No results found."), Times.Once);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(12)]
        [InlineData(130)]
        [InlineData(14)]
        [InlineData(100)]
        public void PrintResult_NRowsAdded_PrintsNRows(int rows)
        {
            string searchTerm = "term";
            Dictionary<ConsoleColor, string> coloredMessage = new Dictionary<ConsoleColor, string>();
            Mock<ILoggerWithColor> mockLoggerWithColor = new Mock<ILoggerWithColor>();
            mockLoggerWithColor.Setup(x => x.LogMinimal(It.IsAny<string>(), It.IsAny<ConsoleColor>()))
                .Callback<string, ConsoleColor>((message, color) =>
                {
                    if (!coloredMessage.ContainsKey(color))
                    {
                        coloredMessage[color] = "";
                    }
                    coloredMessage[color] += message;
                });
            WrappingTable table = new WrappingTable(new int[] { 0, 1, 2, 3 }, new string[] { "column1", "column2", "column3", "column4" });
            var expectedRedColoredMessage = string.Concat(Enumerable.Repeat(searchTerm, rows));
            var expectedDefaultColoredMessage =
                "| column1 | column2 | column3 | column4 |" +
                "| ------- | ------- | ------- | ------- |";
            for (int i = 0; i < rows; i++)
            {
                table.AddRow(new List<string> { "column1", searchTerm, "column3", "column4" });
                expectedDefaultColoredMessage += $"| column1 |     | column3 | column4 |";
                expectedDefaultColoredMessage += "| ------- | ------- | ------- | ------- |";
            }

            // Act
            table.PrintWithHighlighting(mockLoggerWithColor.Object, searchTerm);

            // Assert
            Assert.Equal(expectedRedColoredMessage, coloredMessage[ConsoleColor.Red]);
            Assert.Equal(expectedDefaultColoredMessage, coloredMessage[System.Console.ForegroundColor]);
        }
    }
}
