﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.ProjectManagement
{
    public static class XmlUtility
    {
        public static XDocument GetOrCreateDocument(XName rootName, string root, string path, INuGetProjectContext nuGetProjectContext)
        {
            if (File.Exists(Path.Combine(root, path)))
            {
                try
                {
                    return Shared.XmlUtility.Load(Path.Combine(root, path), LoadOptions.PreserveWhitespace);
                }
                catch (FileNotFoundException) { }
            }

            var document = new XDocument(new XElement(rootName));
            // Add it to the file system
            FileSystemUtility.AddFile(root, path, document.Save, nuGetProjectContext);

            return document;
        }
    }
}
