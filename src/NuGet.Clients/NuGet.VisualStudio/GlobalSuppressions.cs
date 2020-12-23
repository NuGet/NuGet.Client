// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Build", "CA1507:Use nameof in place of string literal 'version'", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SemanticVersion.#ctor(System.Version,System.String,System.String)")]
[assembly: SuppressMessage("Build", "CA1303:Method 'int SemanticVersion.CompareTo(object obj)' passes a literal string as parameter 'message' of a call to 'ArgumentException.ArgumentException(string message)'. Retrieve the following string(s) from a resource table instead: \"obj\".", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SemanticVersion.CompareTo(System.Object)~System.Int32")]
[assembly: SuppressMessage("Build", "CA1507:Use nameof in place of string literal 'version1'", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SemanticVersion.op_GreaterThan(NuGet.SemanticVersion,NuGet.SemanticVersion)~System.Boolean")]
[assembly: SuppressMessage("Build", "CA1507:Use nameof in place of string literal 'version1'", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SemanticVersion.op_LessThan(NuGet.SemanticVersion,NuGet.SemanticVersion)~System.Boolean")]
[assembly: SuppressMessage("Build", "CA1303:Method 'SemanticVersion SemanticVersion.Parse(string version)' passes a literal string as parameter 'message' of a call to 'ArgumentException.ArgumentException(string message, string paramName)'. Retrieve the following string(s) from a resource table instead: \"Value cannot be null or an empty string.\".", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SemanticVersion.Parse(System.String)~NuGet.SemanticVersion")]
[assembly: SuppressMessage("Build", "CA1507:Use nameof in place of string literal 'version'", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SemanticVersion.Parse(System.String)~NuGet.SemanticVersion")]
[assembly: SuppressMessage("Build", "CA1806:ParseOptionalVersion calls TryParse but does not explicitly check whether the conversion succeeded. Either use the return value in a conditional statement or verify that the call site expects that the out argument will be set to the default value when the conversion fails.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SemanticVersion.ParseOptionalVersion(System.String)~NuGet.SemanticVersion")]
