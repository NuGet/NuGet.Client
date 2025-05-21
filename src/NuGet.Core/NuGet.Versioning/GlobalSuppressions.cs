// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Build", "CA1822:Member PrettyPrintBound does not access instance data and can be marked as static (Shared in VisualBasic)", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Versioning.VersionRangeFormatter.PrettyPrintBound(System.Text.StringBuilder,NuGet.Versioning.NuGetVersion,System.Boolean,System.String)")]
[assembly: SuppressMessage("Build", "CA1012:Abstract type VersionRangeBase should not have constructors", Justification = "<Pending>", Scope = "type", Target = "~T:NuGet.Versioning.VersionRangeBase")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Versioning.FloatRange.Parse(System.String)~NuGet.Versioning.FloatRange")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Versioning.ResourcesFormatter.CannotBeNullWhenParameterIsNull(System.String,System.String)~System.ArgumentNullException")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Versioning.ResourcesFormatter.TypeNotSupported(System.Type,System.String)~System.ArgumentException")]
