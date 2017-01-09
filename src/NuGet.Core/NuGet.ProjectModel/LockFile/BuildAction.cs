// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NuGet.ProjectModel
{
    public struct BuildAction : IEquatable<BuildAction>
    {
        private static ConcurrentDictionary<string, BuildAction> _knownBuildActions = new ConcurrentDictionary<string, BuildAction>(StringComparer.OrdinalIgnoreCase);

        public static BuildAction None = Define(nameof(None));
        public static BuildAction Compile = Define(nameof(Compile));
        public static BuildAction Content = Define(nameof(Content));
        public static BuildAction EmbeddedResource = Define(nameof(EmbeddedResource));
        public static BuildAction ApplicationDefinition = Define(nameof(ApplicationDefinition));
        public static BuildAction Page = Define(nameof(Page));
        public static BuildAction Resource = Define(nameof(Resource));
        public static BuildAction SplashScreen = Define(nameof(SplashScreen));
        public static BuildAction DesignData = Define(nameof(DesignData));
        public static BuildAction DesignDataWithDesignTimeCreatableTypes = Define(nameof(DesignDataWithDesignTimeCreatableTypes));
        public static BuildAction CodeAnalysisDictionary = Define(nameof(CodeAnalysisDictionary));
        public static BuildAction AndroidAsset = Define(nameof(AndroidAsset));
        public static BuildAction AndroidResource = Define(nameof(AndroidResource));
        public static BuildAction BundleResource = Define(nameof(BundleResource));

        public string Value { get; }

        public bool IsKnown { get; }

        private BuildAction(string value, bool isKnown)
        {
            Value = value;
            IsKnown = isKnown;
        }

        public static BuildAction Parse(string value)
        {
            BuildAction action;
            if (_knownBuildActions.TryGetValue(value, out action))
            {
                return action;
            }
            return new BuildAction(value, false);
        }

        public override string ToString()
        {
            return $"{Value}";
        }

        public bool Equals(BuildAction other)
        {
            return string.Equals(other.Value, Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is BuildAction && Equals((BuildAction)obj);
        }

        public static bool operator ==(BuildAction left, BuildAction right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BuildAction left, BuildAction right)
        {
            return !Equals(left, right);
        }

        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return 0;
            }
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        private static BuildAction Define(string name)
        {
            var buildAction = new BuildAction(name, true);
            _knownBuildActions[name] = buildAction;
            return buildAction;
        }
    }
}