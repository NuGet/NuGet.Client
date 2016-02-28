// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.Versioning;

namespace NuGet.Packaging
{
    public class PhysicalPackageFile : IPackageFile
    {
        private readonly Func<Stream> _streamFactory;
        private string _targetPath;
        private FrameworkName _targetFramework;

        public PhysicalPackageFile()
        {
        }

        internal PhysicalPackageFile(Func<Stream> streamFactory)
        {
            _streamFactory = streamFactory;
        }

        /// <summary>
        /// Path on disk
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// Path in package
        /// </summary>
        public string TargetPath
        {
            get
            {
                return _targetPath;
            }
            set
            {
                if (String.Compare(_targetPath, value, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    _targetPath = value;
                    string effectivePath;
                    _targetFramework = FrameworkNameUtility.ParseFrameworkNameFromFilePath(_targetPath, out effectivePath);
                    EffectivePath = effectivePath;
                }
            }
        }

        public string Path
        {
            get
            {
                return TargetPath;
            }
        }

        public string EffectivePath
        {
            get;
            private set;
        }

        public FrameworkName TargetFramework
        {
            get { return _targetFramework; }
        }

        public Stream GetStream()
        {
            return _streamFactory != null ? _streamFactory() : File.OpenRead(SourcePath);
        }

        public override string ToString()
        {
            return TargetPath;
        }

        public override bool Equals(object obj)
        {
            var file = obj as PhysicalPackageFile;

            return file != null && String.Equals(SourcePath, file.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                                   String.Equals(TargetPath, file.TargetPath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            int hash = 0;
            if (SourcePath != null)
            {
                hash = SourcePath.GetHashCode();
            }

            if (TargetPath != null)
            {
                hash = hash * 4567 + TargetPath.GetHashCode();
            }

            return hash;
        }
    }
}
