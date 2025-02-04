// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET9_0
global using ArgumentOfString = System.CommandLine.CliArgument<string>;
global using Command = System.CommandLine.CliCommand;
global using CommandLineConfiguration = System.CommandLine.CliConfiguration;
global using OptionOfBoolean = System.CommandLine.CliOption<bool>;
global using OptionOfString = System.CommandLine.CliOption<string>;
global using OptionOfListOfStrings = System.CommandLine.CliOption<System.Collections.Generic.List<string>>;
global using TokenType = System.CommandLine.Parsing.CliTokenType;
#else
global using ArgumentOfString = System.CommandLine.Argument<string>;
global using Command = System.CommandLine.Command;
global using CommandLineConfiguration = System.CommandLine.CommandLineConfiguration;
global using OptionOfString = System.CommandLine.Option<string>;
global using OptionOfBoolean = System.CommandLine.Option<bool>;
global using OptionOfListOfStrings = System.CommandLine.Option<System.Collections.Generic.List<string>>;
global using TokenType = System.CommandLine.Parsing.TokenType;
#endif
