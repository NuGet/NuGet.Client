// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Markdown.Platform;
using Microsoft.VisualStudio.Text.Editor;

namespace NuGet.PackageManagement.UI
{
    [Export(typeof(MarkdownPreviewSingleton))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MarkdownPreviewSingleton
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private static IMarkdownPreview Instance;
#pragma warning restore CS0618 // Type or member is obsolete

        [Import(typeof(IEditorOptionsFactoryService))]
        private IEditorOptionsFactoryService EditorOptionsFactoryService { get; set; }

#pragma warning disable CS0618 // Type or member is obsolete
        public IMarkdownPreview GetInstance()
#pragma warning restore CS0618 // Type or member is obsolete
        {
            if (Instance == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var previewBuilder = new PreviewBuilder();
#pragma warning restore CS0618 // Type or member is obsolete
                previewBuilder.EditorOptions = EditorOptionsFactoryService.GlobalOptions;

                Instance = previewBuilder.Build();
            }

            return Instance;
        }

        public static void ResetInstance()
        {
            if (Instance != null)
            {
                Instance.Dispose();
                Instance = null;
            }
        }
    }
}
