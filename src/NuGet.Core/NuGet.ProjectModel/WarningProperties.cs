// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Class to hold warning properties given by project system.
    /// </summary>
    public class WarningProperties : IEquatable<WarningProperties>
    {
        /// <summary>
        /// List of Warning Codes that should be treated as Errors.
        /// </summary>
        public ISet<NuGetLogCode> WarningsAsErrors { get; }

        /// <summary>
        /// List of Warning Codes that should be ignored.
        /// </summary>
        public ISet<NuGetLogCode> NoWarn { get; }

        /// <summary>
        /// Indicates if all warnings should be ignored.
        /// </summary>
        public bool AllWarningsAsErrors { get; set; }

        public WarningProperties()
        {
            WarningsAsErrors = new HashSet<NuGetLogCode>();
            NoWarn = new HashSet<NuGetLogCode>();
            AllWarningsAsErrors = false;
    }

        public WarningProperties(ISet<NuGetLogCode> warningsAsErrors, ISet<NuGetLogCode> noWarn, bool allWarningsAsErrors)
            : base()
        {
            WarningsAsErrors = warningsAsErrors ?? throw new ArgumentNullException(nameof(warningsAsErrors));
            NoWarn = noWarn ?? throw new ArgumentNullException(nameof(noWarn));
            AllWarningsAsErrors = allWarningsAsErrors;
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddSequence(WarningsAsErrors);
            hashCode.AddSequence(NoWarn);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WarningProperties);
        }

        public bool Equals(WarningProperties other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return AllWarningsAsErrors == other.AllWarningsAsErrors &&
                EqualityUtility.SetEqualsWithNullCheck(WarningsAsErrors, other.WarningsAsErrors) &&
                EqualityUtility.SetEqualsWithNullCheck(NoWarn, other.NoWarn);
        }

        public WarningProperties Clone()
        {
            return new WarningProperties(warningsAsErrors: new HashSet<NuGetLogCode>(WarningsAsErrors), noWarn: new HashSet<NuGetLogCode>(NoWarn), allWarningsAsErrors: AllWarningsAsErrors);
        }
    }
}
