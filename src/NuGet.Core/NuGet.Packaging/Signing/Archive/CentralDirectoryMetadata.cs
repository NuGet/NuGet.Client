using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// This class is used to hold metatdata about the central directory archive structure
    /// </summary>
    public class CentralDirectoryMetadata
    {
        /// <summary>
        /// Position of the corresponding central directory header
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Offset to the corresponding file header
        /// </summary>
        public long OffsetToFileHeader { get; set; }

        /// <summary>
        /// Total size of corresponding file entry
        /// This should include size of local file header + encryption header + file data + data descriptor
        /// </summary>
        public long FileEntryTotalSize { get; set; }

        /// <summary>
        /// Filename for the central directory header
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Size of central directory header
        /// </summary>
        public long HeaderSize { get; set; }

        /// <summary>
        /// Value used to identify how much the position of the OffsetToFileHeader property will change by
        /// the presence of a signature file
        /// </summary>
        public long ChangeInOffset { get; set; }

        /// <summary>
        /// Index in which the central directory record was read from the archive.
        /// This index represents the order of de central directory record as it is in the file.
        /// </summary>
        public int IndexInRecords { get; set; }
    }
}
