﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Media;

namespace NuGetConsole
{
    /// <summary>
    /// Represents read-only console used to log output messages
    /// </summary>
    public interface IOutputConsole
    {
        /// <summary>
        /// Activate the console window.
        /// </summary>
        void Activate();

        /// <summary>
        /// Clear the console content.
        /// </summary>
        void Clear();

        /// <summary>
        /// Get the console width measured by chars.
        /// </summary>
        int ConsoleWidth { get; }

        /// <summary>
        /// Display progress data for the current command
        /// </summary>
        void WriteProgress(string currentOperation, int percentComplete);

        /// <summary>
        /// Write text to the console.
        /// </summary>
        /// <param name="text">The text content.</param>
        void Write(string text);

        /// <summary>
        /// Write a line of text to the console. This appends a newline to text content.
        /// </summary>
        /// <param name="text">The text content.</param>
        void WriteLine(string text);

        /// <summary>
        /// Write formatted line of text to the console. This appends a newline to text content.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">Format string parameters</param>
        void WriteLine(string format, params object[] args);

        /// <summary>
        /// Write text to the console with color.
        /// </summary>
        /// <param name="text">The text content.</param>
        /// <param name="foreground">Optional foreground color.</param>
        /// <param name="background">Optional background color.</param>
        void Write(string text, Color? foreground, Color? background);

        /// <summary>
        /// Delete the last character from the current line and move the caret back one char.
        /// </summary>
        void WriteBackspace();
    }
}
