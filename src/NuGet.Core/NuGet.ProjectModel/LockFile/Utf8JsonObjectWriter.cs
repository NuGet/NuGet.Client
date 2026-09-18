// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NuGet.RuntimeModel;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Adapts a <see cref="Utf8JsonWriter" /> to the neutral <see cref="IObjectWriter" /> contract.
    /// </summary>
    /// <remarks>
    /// This is a readonly value type so generic writer pipelines can preserve its concrete type. This allows the
    /// .NET Framework JIT to use value-type-specific code and constrained interface calls, avoiding boxing and
    /// repeated interface dispatch. Passing this writer through a non-generic <see cref="IObjectWriter" /> parameter
    /// would box it and lose that performance benefit.
    /// </remarks>
    internal readonly struct Utf8JsonObjectWriter : IObjectWriter
    {
        private static readonly string[] EncodedPropertyNameKeys;
        private static readonly JsonEncodedText[] EncodedPropertyNameValues;

        private readonly Utf8JsonWriter _writer;

        static Utf8JsonObjectWriter()
        {
            CreateEncodedPropertyNames(out EncodedPropertyNameKeys, out EncodedPropertyNameValues);
        }

        internal Utf8JsonObjectWriter(Utf8JsonWriter writer)
        {
            _writer = writer;
        }

        public void WriteObjectStart()
        {
            _writer.WriteStartObject();
        }

        public void WriteObjectStart(string name)
        {
            WritePropertyName(name);
            _writer.WriteStartObject();
        }

        public void WriteObjectEnd()
        {
            _writer.WriteEndObject();
        }

        public void WriteNameValue(string name, int value)
        {
            if (TryGetEncodedPropertyName(name, out JsonEncodedText encodedName))
            {
                _writer.WriteNumber(encodedName, value);
            }
            else
            {
                _writer.WriteNumber(name, value);
            }
        }

        public void WriteNameValue(string name, bool value)
        {
            if (TryGetEncodedPropertyName(name, out JsonEncodedText encodedName))
            {
                _writer.WriteBoolean(encodedName, value);
            }
            else
            {
                _writer.WriteBoolean(name, value);
            }
        }

        public void WriteNameValue(string name, string value)
        {
            if (TryGetEncodedPropertyName(name, out JsonEncodedText encodedName))
            {
                _writer.WriteString(encodedName, value);
            }
            else
            {
                _writer.WriteString(name, value);
            }
        }

        public void WriteNameArray(string name, IEnumerable<string> values)
        {
            WritePropertyName(name);
            _writer.WriteStartArray();

            foreach (string value in values)
            {
                _writer.WriteStringValue(value);
            }

            _writer.WriteEndArray();
        }

        public void WriteNonEmptyNameArray(string name, IEnumerable<string> values)
        {
            using var enumerator = values.NoAllocEnumerate().GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return;
            }

            WritePropertyName(name);
            _writer.WriteStartArray();
            _writer.WriteStringValue(enumerator.Current);

            while (enumerator.MoveNext())
            {
                _writer.WriteStringValue(enumerator.Current);
            }

            _writer.WriteEndArray();
        }

        public void WriteArrayStart(string name)
        {
            WritePropertyName(name);
            _writer.WriteStartArray();
        }

        public void WriteArrayEnd()
        {
            _writer.WriteEndArray();
        }

        /// <summary>
        /// Creates a lookup from known schema property-name literals to their pre-encoded UTF-8 representations.
        /// </summary>
        /// <remarks>
        /// The writer call sites use interned string literals for these fixed property names, so reference identity
        /// provides a fast lookup without repeatedly encoding names or calculating string hashes. A compact,
        /// power-of-two open-addressed table avoids the additional overhead of a dictionary, which is significant
        /// for the .NET Framework writer path. Dynamic property names do not match by reference and continue through
        /// the normal <see cref="Utf8JsonWriter" /> string overloads, preserving their standard encoding behavior.
        /// </remarks>
        private static void CreateEncodedPropertyNames(out string[] keys, out JsonEncodedText[] values)
        {
            string[] names =
            [
                "#import",
                "aliases",
                "allWarningsAsErrors",
                "assetTargetFallback",
                "auditLevel",
                "auditMode",
                "autoReferenced",
                "centralPackageFloatingVersionsEnabled",
                "centralPackageVersionOverrideDisabled",
                "centralPackageVersions",
                "centralPackageVersionsManagementEnabled",
                "CentralPackageTransitivePinningEnabled",
                "configFilePaths",
                "crossTargeting",
                "dependencies",
                "downloadDependencies",
                "enableAudit",
                "excludeAssets",
                "fallbackFolders",
                "files",
                "format",
                "framework",
                "frameworkAssemblies",
                "frameworkReferences",
                "frameworks",
                "generatePathProperty",
                "imports",
                "include",
                "includeAssets",
                "legacyPackagesDirectory",
                "name",
                "noWarn",
                "nuGetLockFilePath",
                "originalTargetFrameworks",
                "outputPath",
                "packagesConfigPath",
                "packagesPath",
                "packagesToPrune",
                "privateAssets",
                "projectJsonPath",
                "projectName",
                "projectPath",
                "projectReferences",
                "projects",
                "projectStyle",
                "projectUniqueName",
                "restore",
                "restoreAuditProperties",
                "restoreDoNotWriteDependencyGraphSpec",
                "restoreEnableAnalyzerAssets",
                "restoreLockedMode",
                "restoreLockProperties",
                "restorePackagesWithLockFile",
                "restoreUseLegacyDependencyResolver",
                "runtimes",
                "runtimeIdentifierGraphPath",
                "SdkAnalysisLevel",
                "secondaryFramework",
                "skipContentFileWrite",
                "sources",
                "supports",
                "suppressedAdvisories",
                "suppressParent",
                "target",
                "targetAlias",
                "UsingMicrosoftNETSdk",
                "validateRuntimeAssets",
                "version",
                "versionCentrallyManaged",
                "versionOverride",
                "warn",
                "warnAsError",
                "warningProperties",
                "warnNotAsError",
            ];

            keys = new string[256];
            values = new JsonEncodedText[keys.Length];

            foreach (string name in names)
            {
                int index = RuntimeHelpers.GetHashCode(name) & (keys.Length - 1);
                while (keys[index] != null)
                {
                    index = (index + 1) & (keys.Length - 1);
                }

                keys[index] = name;
                values[index] = JsonEncodedText.Encode(name);
            }
        }

        private void WritePropertyName(string name)
        {
            if (TryGetEncodedPropertyName(name, out JsonEncodedText encodedName))
            {
                _writer.WritePropertyName(encodedName);
            }
            else
            {
                _writer.WritePropertyName(name);
            }
        }

        private static bool TryGetEncodedPropertyName(string name, out JsonEncodedText encodedName)
        {
            if (name != null)
            {
                int index = RuntimeHelpers.GetHashCode(name) & (EncodedPropertyNameKeys.Length - 1);
                while (EncodedPropertyNameKeys[index] != null)
                {
                    if (ReferenceEquals(EncodedPropertyNameKeys[index], name))
                    {
                        encodedName = EncodedPropertyNameValues[index];
                        return true;
                    }

                    index = (index + 1) & (EncodedPropertyNameKeys.Length - 1);
                }
            }

            encodedName = default;
            return false;
        }
    }
}
