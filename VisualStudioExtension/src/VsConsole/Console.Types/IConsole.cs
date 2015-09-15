// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Media;

namespace NuGetConsole
{
    /// <summary>
    /// Represents a console (editor) which a user interacts with.
    /// </summary>
    public interface IConsole
    {
        /// <summary>
        /// The host associated with this console. Each console is associated with 1 host
        /// to perform command interpretation. The console creator is responsible to
        /// setup this association.
        /// </summary>
        IHost Host { get; set; }

        /// <summary>
        /// Indicates to the active host whether this console wants to show a disclaimer header.
        /// </summary>
        bool ShowDisclaimerHeader { get; }

        /// <summary>
        /// Get the console dispatcher which dispatches user interaction.
        /// </summary>
        IConsoleDispatcher Dispatcher { get; }

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

        /// <summary>
        /// Clear the console content.
        /// Note that this can only be called in a user command execution. If you need to
        /// clear console from outside user command execution, use Dispatcher.ClearConsole.
        /// </summary>
        void Clear();
    }
}
