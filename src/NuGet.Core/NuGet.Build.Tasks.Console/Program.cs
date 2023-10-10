// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
#if NETFRAMEWORK
using System.Reflection;
#endif
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Build.Tasks.Console
{
    /// <summary>
    /// Represents the main entry point to the console application.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// A <see cref="T:char[]" /> containing the equals sign '=' to be used to split key/value pairs that are separated by it.
        /// </summary>
        private static readonly char[] EqualSign = { '=' };

        /// <summary>
        /// A <see cref="T:char[]" /> containing the semicolon ';' to be used to split key/value pairs that are separated by it.
        /// </summary>
        private static readonly char[] Semicolon = { ';' };

        /// <summary>
        /// The main entry point to the console application.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns><code>0</code> if the application ran successfully with no errors, otherwise <code>1</code>.</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var debug = IsDebug();

                if (debug)
                {
                    Debugger.Launch();
                }

                NuGet.Common.Migrations.MigrationRunner.Run();

                // Parse command-line arguments
                if (!TryParseArguments(args, () => System.Console.OpenStandardInput(), System.Console.Error, out (Dictionary<string, string> Options, FileInfo MSBuildExeFilePath, string EntryProjectFilePath, Dictionary<string, string> MSBuildGlobalProperties) arguments))
                {
                    return 1;
                }

                // Enable MSBuild feature flags
                MSBuildFeatureFlags.MSBuildExeFilePath = arguments.MSBuildExeFilePath.FullName;
                MSBuildFeatureFlags.EnableCacheFileEnumerations = true;
                MSBuildFeatureFlags.LoadAllFilesAsReadonly = true;
                MSBuildFeatureFlags.SkipEagerWildcardEvaluations = true;
#if NETFRAMEWORK
                if (AppDomain.CurrentDomain.IsDefaultAppDomain())
                {
                    // MSBuild.exe.config has binding redirects that change from time to time and its very hard to make sure that NuGet.Build.Tasks.Console.exe.config is correct.
                    // It also can be different per instance of Visual Studio so when running unit tests it always needs to match that instance of MSBuild
                    // The code below runs this EXE in an AppDomain as if its MSBuild.exe so the assembly search location is next to MSBuild.exe and all binding redirects are used
                    // allowing this process to evaluate MSBuild projects as if it is MSBuild.exe
                    Assembly thisAssembly = typeof(Program).Assembly;

                    AppDomain appDomain = AppDomain.CreateDomain(
                        thisAssembly.FullName,
                        securityInfo: null,
                        info: new AppDomainSetup
                        {
                            ApplicationBase = arguments.MSBuildExeFilePath.DirectoryName,
                            ConfigurationFile = Path.Combine(arguments.MSBuildExeFilePath.DirectoryName, "MSBuild.exe.config")
                        });

                    return appDomain
                        .ExecuteAssembly(
                            thisAssembly.Location,
                            args);
                }
#endif

                // Check whether the ask is to generate the restore graph file.
                if (MSBuildStaticGraphRestore.IsOptionTrue("GenerateRestoreGraphFile", arguments.Options))
                {
                    using (var dependencyGraphSpecGenerator = new MSBuildStaticGraphRestore(debug: debug))
                    {
                        return dependencyGraphSpecGenerator.WriteDependencyGraphSpec(arguments.EntryProjectFilePath, arguments.MSBuildGlobalProperties, arguments.Options) ? 0 : 1;
                    }
                }

                // Otherwise run restore!
                using (var dependencyGraphSpecGenerator = new MSBuildStaticGraphRestore(debug: debug))
                {
                    return await dependencyGraphSpecGenerator.RestoreAsync(arguments.EntryProjectFilePath, arguments.MSBuildGlobalProperties, arguments.Options) ? 0 : 1;
                }
            }
            catch (Exception e)
            {
                var consoleOutLogMessage = new ConsoleOutLogMessage
                {
                    Message = string.Format(CultureInfo.CurrentCulture, Strings.Error_StaticGraphUnhandledException, e.ToString()),
                    MessageType = ConsoleOutLogMessageType.Error,
                };

                System.Console.Out.WriteLine(consoleOutLogMessage.ToJson());

                return -1;
            }
        }

        /// <summary>
        /// Determines if a user specified that the current process is being debugged.
        /// </summary>
        /// <returns><code>true</code> if the user specified to debug the current process, otherwise <code>false</code>.</returns>
        private static bool IsDebug()
        {
            return string.Equals(Environment.GetEnvironmentVariable("DEBUG_RESTORE_TASK"), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a semicolon delimited list of equal sign separated key value pairs.
        /// </summary>
        /// <param name="value">The string containing a semicolon delimited list of key value pairs to parse.</param>
        /// <returns>A <see cref="Dictionary{String,String}" /> containing the list of items as key value pairs.</returns>
        private static Dictionary<string, string> ParseSemicolonDelimitedListOfKeyValuePairs(string value)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in value
                .Split(Semicolon, StringSplitOptions.RemoveEmptyEntries)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Split(EqualSign, 2))
                .Where(i => i.Length == 2 && !string.IsNullOrWhiteSpace(i[0]) && !string.IsNullOrWhiteSpace(i[1])))
            {
                properties[pair[0].Trim()] = pair[1].Trim();
            }

            return properties;
        }

        /// <summary>
        /// Parses command-line arguments.
        /// </summary>
        /// <param name="args">A <see cref="T:string[]" /> containing the process command-line arguments.</param>
        /// <param name="getStream">A <see cref="Func{TResult}" /> that is called to get a <see cref="Stream" /> to read.</param>
        /// <param name="errorWriter">A <see cref="TextWriter" /> to write errors to.</param>
        /// <param name="arguments">A <see cref="T:Tuple&lt;Dictionary&lt;string, string&gt;, FileInfo, string, Dictionary&lt;string, string&gt;&gt;" /> that receives the parsed command-line arguments.</param>
        /// <returns><code>true</code> if the arguments were successfully parsed, otherwise <code>false</code>.</returns>
        private static bool TryParseArguments(string[] args, Func<Stream> getStream, TextWriter errorWriter, out (Dictionary<string, string> Options, FileInfo MSBuildExeFilePath, string EntryProjectFilePath, Dictionary<string, string> MSBuildGlobalProperties) arguments)
        {
            arguments = (null, null, null, null);

            // This application recieves 3 or 4 arguments:
            // 1. A semicolon delimited list of key value pairs that are the options to the program
            // 2. The full path to MSBuild.exe
            // 3. The full path to the entry project file
            // 4. (optional) A semicolon delimited list of key value pairs that are the global properties to pass to MSBuild
            if (args.Length != 3 && args.Length != 4)
            {
                // An error occurred parsing command-line arguments in static graph-based restore as there was an unexpected number of arguments, {0}. Please file an issue at https://github.com/NuGet/Home. {0}
                return LogError(System.Console.Error, Strings.Error_StaticGraphRestoreArgumentParsingFailedInvalidNumberOfArguments, args.Length);
            }

            try
            {
                Dictionary<string, string> options = ParseSemicolonDelimitedListOfKeyValuePairs(args[0]);
                var msbuildExeFilePath = new FileInfo(args[1]);
                var entryProjectFilePath = args[2];

                Dictionary<string, string> globalProperties = null;

                // If there are 3 arguments then the global properties will be read from STDIN
                if (args.Length == 3)
                {
#if NETFRAMEWORK
                    if (AppDomain.CurrentDomain.IsDefaultAppDomain())
                    {
                        // The application is called twice and the first invocation does not need to read the global properties
                        globalProperties = null;
                    }
                    else
                    {
#endif
                        using var reader = new BinaryReader(getStream());

                        if (!TryDeserializeGlobalProperties(errorWriter, reader, out globalProperties))
                        {
                            // An error will have already been logged by TryDeserializeGlobalProperties()
                            return false;
                        }
#if NETFRAMEWORK
                    }
#endif
                }
                else
                {
                    globalProperties = ParseSemicolonDelimitedListOfKeyValuePairs(args[3]);
                }

                arguments = (options, msbuildExeFilePath, entryProjectFilePath, globalProperties);

                // Command-line is correct if no exceptions were thrown and the MSBuild path exists and an entry project were specified
                return msbuildExeFilePath.Exists && !string.IsNullOrWhiteSpace(entryProjectFilePath);
            }
            catch (Exception)
            {
                arguments = (null, null, null, null);

                return false;
            }
        }

        /// <summary>
        /// Logs an error to the specified <see cref="TextWriter" />.
        /// </summary>
        /// <param name="errorWriter">The <see cref="TextWriter" /> to write the error to.</param>
        /// <param name="format">The formatted string of the error.</param>
        /// <param name="args">An object array of zero or more objects to format with the error message.</param>
        /// <returns><see langword="false" /></returns>
        private static bool LogError(TextWriter errorWriter, string format, params object[] args)
        {
            errorWriter.WriteLine(format, args);

            return false;
        }

        /// <summary>
        /// Attempts to deserialize global properties from the standard input stream.
        /// </summary>
        /// <remarks>
        /// The stream backed by the specified <see cref="BinaryReader" /> may contain a preamble/byte order mark.   Preambles are variable length
        /// from 2 to 4 bytes.  The first 4 bytes are either:
        ///   -Variable length preamble and any remaining bytes of the actual content
        ///   -No preamble and just the integer representing the number of items in the dictionary
        ///
        /// Since the preamble could be 3 bytes, that means that the last byte in the buffer will be the first byte of the next segment. So this code
        /// determines how long the preamble is, replaces the remaining bytes at the beginning of the buffer, and copies the next set of bytes.  This
        /// effectively "skips" the preamble by eventually having the buffer contain the first 4 bytes of the content that should actually be read.
        ///
        /// Example stream:
        /// 
        /// |   3 byte preamble  |      4 byte integer       |
        /// |------|------|------|------|------|------|------|
        /// | 0xFF | 0XEF | 0x00 | 0x07 | 0x00 | 0x00 | 0x00 |
        ///
        /// The first 4 bytes are read into the buffer (notice one of the bytes is actually not the preamble):
        /// 
        /// |       4 byte buffer       |
        /// |------|------|------|------|
        /// | 0xFF | 0XEF | 0x00 | 0x07 |
        ///
        /// If the first three bytes match the preamble (0xFF, 0xEF, 0x00), then the array is shifted so that last item becomes the first:
        /// 
        /// |       4 byte buffer       |
        /// |------|------|------|------|
        /// | 0x07 | 0XEF | 0x00 | 0x07 |
        ///
        /// Since only 1 byte was moved up in the buffer, then the next 3 bytes from the stream are read:
        ///
        /// |       4 byte buffer       |
        /// |------|------|------|------|
        /// | 0x07 | 0X00 | 0x00 | 0x00 |
        ///
        /// Now the buffer contains the integer in the stream and the preamble is skipped.
        /// 
        /// </remarks>
        /// <param name="errorWriter">A <see cref="TextWriter" /> to write errors to if one occurs.</param>
        /// <param name="reader">The <see cref="BinaryReader" /> to use when deserializing the arguments.</param>
        /// <param name="globalProperties">Receives a <see cref="Dictionary{TKey, TValue}" /> representing the global properties.</param>
        /// <returns><see langword="true" /> if the arguments were successfully deserialized, otherwise <see langword="false" />.</returns>
        internal static bool TryDeserializeGlobalProperties(TextWriter errorWriter, BinaryReader reader, out Dictionary<string, string> globalProperties)
        {
            globalProperties = null;

            byte[] buffer = new byte[4];

            try
            {
                // Attempt to read the first 4 bytes into the buffer which contain a variable length preamble or an integer representing the number of items in the dictionary
                if (reader.Read(buffer, 0, 4) != buffer.Length)
                {
                    // An error occurred parsing command-line arguments in static graph-based restore as end of the standard input stream was unexpectedly encountered. Please file an issue at https://github.com/NuGet/Home
                    return LogError(errorWriter, Strings.Error_StaticGraphRestoreArgumentsParsingFailedEndOfStream);
                }
            }
            catch (Exception e)
            {
                // An error occurred parsing command-line arguments in static graph-based restore as an exception occurred reading the standard input stream, {0}. Please file an issue at https://github.com/NuGet/Home
                return LogError(errorWriter, Strings.Error_StaticGraphRestoreArgumentsParsingFailedExceptionReadingStream, e.Message, e.ToString());
            }

            using (MemoryStream memoryStream = new MemoryStream(buffer, writable: false))
            using (StreamReader streamReader = new StreamReader(memoryStream, Encoding.Default, detectEncodingFromByteOrderMarks: true, bufferSize: buffer.Length))
            {
                // The call to Read() here will detect the preamble in the buffer by populating streamReader.CurrentEncoding
                int result = streamReader.Read();

                byte[] preamble = streamReader.CurrentEncoding.GetPreamble();

                if (preamble.Length != 0)
                {
                    // Copies the bytes from beyond the preamble to the beginning of the buffer
                    Array.Copy(buffer, preamble.Length, buffer, 0, buffer.Length - preamble.Length);

                    try
                    {
                        // Reads in next bytes from the BinaryReader to fill the rest of the buffer so that it contains the integer representing the number of items in the dictionary
                        if (reader.Read(buffer, buffer.Length - preamble.Length, preamble.Length) != preamble.Length)
                        {
                            // An error occurred parsing command-line arguments in static graph-based restore as end of the standard input stream was unexpectedly encountered. Please file an issue at https://github.com/NuGet/Home
                            return LogError(errorWriter, Strings.Error_StaticGraphRestoreArgumentsParsingFailedEndOfStream);
                        }
                    }
                    catch (Exception e)
                    {
                        return LogError(errorWriter, Strings.Error_StaticGraphRestoreArgumentsParsingFailedExceptionReadingStream, e.Message, e.ToString());
                    }
                }
            }

            // Convert the bytes in the buffer to an integer
            int length = BitConverter.ToInt32(buffer, 0);

            // If the integer is negative or greater than or equal to int.MaxValue, then the integer is invalid.  This should never happen unless the bytes in the stream contain completely unexpected values
            if (length < 0 || length >= int.MaxValue)
            {
                // An error occurred parsing command-line arguments in static graph-based restore as the first integer read, {0}, was is greater than the allowable value. Please file an issue at https://github.com/NuGet/Home
                return LogError(errorWriter, Strings.Error_StaticGraphRestoreArgumentsParsingFailedUnexpectedIntegerValue, length);
            }

            globalProperties = new Dictionary<string, string>(length, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < length; i++)
            {
                try
                {
                    globalProperties[reader.ReadString()] = reader.ReadString();
                }
                catch (Exception e)
                {
                    globalProperties = null;

                    // An error occurred parsing command-line arguments in static graph-based restore as an exception occurred reading the standard input stream, {0}. Please file an issue at https://github.com/NuGet/Home
                    return LogError(errorWriter, Strings.Error_StaticGraphRestoreArgumentsParsingFailedExceptionReadingStream, e.Message, e.ToString());
                }
            }

            return true;
        }
    }
}
