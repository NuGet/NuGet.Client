// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using MessagePack;
using MessagePack.Formatters;
using Microsoft;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;

namespace NuGet.VisualStudio.Internal.Contracts
{
    internal sealed class ProjectActionFormatter : IMessagePackFormatter<ProjectAction?>
    {
        private const string IdPropertyName = "id";
        private const string ImplicitActionsPropertyName = "implicitactions";
        private const string PackageIdentityPropertyName = "packageidentity";
        private const string ProjectActionTypePropertyName = "projectactiontype";
        private const string ProjectIdPropertyName = "projectid";

        internal static readonly IMessagePackFormatter<ProjectAction?> Instance = new ProjectActionFormatter();

        private ProjectActionFormatter()
        {
        }

        public ProjectAction? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
            options.Security.DepthStep(ref reader);

            try
            {
                string? id = null;
                ImplicitProjectAction[]? implicitActions = null;
                PackageIdentity? packageIdentity = null;
                NuGetProjectActionType? projectActionType = null;
                string? projectId = null;

                int propertyCount = reader.ReadMapHeader();

                for (var propertyIndex = 0; propertyIndex < propertyCount; ++propertyIndex)
                {
                    switch (reader.ReadString())
                    {
                        case IdPropertyName:
                            id = reader.ReadString();
                            break;

                        case ImplicitActionsPropertyName:
                            int elementCount = reader.ReadArrayHeader();
                            implicitActions = new ImplicitProjectAction[elementCount];

                            for (var i = 0; i < elementCount; ++i)
                            {
                                ImplicitProjectAction? implicitAction = ImplicitProjectActionFormatter.Instance.Deserialize(ref reader, options);

                                Assumes.NotNull(implicitAction);

                                implicitActions[i] = implicitAction;
                            }
                            break;

                        case PackageIdentityPropertyName:
                            packageIdentity = PackageIdentityFormatter.Instance.Deserialize(ref reader, options);
                            break;

                        case ProjectActionTypePropertyName:
                            projectActionType = options.Resolver.GetFormatter<NuGetProjectActionType>().Deserialize(ref reader, options);
                            break;

                        case ProjectIdPropertyName:
                            projectId = reader.ReadString();
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }

                Assumes.NotNullOrEmpty(id);
                Assumes.NotNull(packageIdentity);
                Assumes.True(projectActionType.HasValue);
                Assumes.NotNullOrEmpty(projectId);

                return new ProjectAction(id, projectId, packageIdentity, projectActionType.Value, implicitActions);
            }
            finally
            {
                // stack overflow mitigation - see https://github.com/neuecc/MessagePack-CSharp/security/advisories/GHSA-7q36-4xx7-xcxf
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, ProjectAction? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(count: 5);
            writer.Write(IdPropertyName);
            writer.Write(value.Id);
            writer.Write(PackageIdentityPropertyName);
            PackageIdentityFormatter.Instance.Serialize(ref writer, value.PackageIdentity, options);
            writer.Write(ProjectActionTypePropertyName);
            options.Resolver.GetFormatter<NuGetProjectActionType>().Serialize(ref writer, value.ProjectActionType, options);
            writer.Write(ProjectIdPropertyName);
            writer.Write(value.ProjectId);
            writer.Write(ImplicitActionsPropertyName);
            writer.WriteArrayHeader(value.ImplicitActions.Count);

            foreach (ImplicitProjectAction action in value.ImplicitActions)
            {
                ImplicitProjectActionFormatter.Instance.Serialize(ref writer, action, options);
            }
        }
    }
}
