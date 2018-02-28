using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NuGet.Tests.Foundation.Utility.Diagnostics;



namespace NuGet.Tests.Foundation.Utility.IO
{
    public static class PathHelper
    {
        //  Details on long (>MAX_PATH) filenames
        //  -------------------------------------
        //
        //  Kim Hamilton has done a writeup on these on the BCL team blog. "Long Paths in .NET"
        //
        //  Long paths are specified by prepending the path w	ith "\\?\" or "\\?\UNC\" for UNC paths.
        //  The .NET Framework does not handle long paths.  In order to support long paths we would
        //  have to fully implement our own IO functionality.
        //
        //  One other possibility would be to use short filename versions when the paths are too
        //  long.  We would still have to write significant amounts of code to do this and .NET
        //  could still choke in places that it tries to normalize the paths.
        //
        //  Paths of the \\?\ syntax can NOT be relative (per the SDK).  They cannot use forward
        //  slashes or period to represent the current directory.

        //  Path formats
        //  ------------
        //
        //  Drive absolute                     C:\rest_of_path
        //                                     \\?\C:\rest_of_path
        //  Drive relative                     C:rest_of_path
        //  Rooted                             \rest_of_path
        //  Local device                       \\.\rest_of_path   (ex: \\.\CON)
        //                                     \\?\rest_of_path
        //  Rooted local device                \\.{NULL}
        //                                     \\?{NULL}
        //  UNC absolute                       \\server\share
        //                                     \\?\UNC\server\share
        //  Relative                           rest_of_path

        //  Yet another wrinkle...  Parallels for the Mac uses the following format for Desktop Sharing:
        //
        //    '\\.psf\Home\Documents\Expression\Blend 4 Beta\Samples\Zune3D\Zune3D.sln'
        //
        //  Need to allow for this in our validation code.

        //  Filename resolution
        //  -------------------
        //
        //  After the the format specifier (see above) paths are a series of optional directory names
        //  separated by directory separators ('\' or '/').  The name after the final separator (if any)
        //  can represent a filename or a directory name.
        //
        //  Names can start with spaces ' ' or dots '.'.  There are two special cases for dots: (1) if
        //  the name is comprised of only a single dot it represents the current directory and (2) if
        //  the name is comprised of just two dots '..' it is represents the parent directory.  (It
        //  removes the directory name before it (if any) from the full path)
        //
        //  ("C:\Foo\.." == "C:\Foo\..\..\.." == "C:")
        //
        //  While it is possible to have trailing spaces ' 'or dots '.' in names they are difficult to
        //  create and access and should be avoided.  Various parts of the OS deal with these differently.
        //  If you do things such as add '.\foo' to the end of 'C:\Bar.' you'll end up with failing or
        //  (worse) unpredictable method calls.  As this is the case all methods here consider this an
        //  invalid state.  If you wish to be friendly to incoming paths from external sources consider
        //  trimming trailing spaces and periods.
        //
        //  Device names are names in the following set: CON, PRN, AUX, NUL, COM[1..9], LPT[1..9]
        //  Any name that starts with a device name followed by a dot is considered to be said device
        //  name ("CON.TXT.FOO" => "CON") any other character is a standard name ("CONN.TXT.FOO" =>
        //  "CONN.TXT.FOO").
        //
        //  Device names cannot be used in a path.

        //  Path resolution functionality can be found at \base\win32\client\curdir.c & vdm.c
        //  Of note: RtlGetFullPathName_Ustr

        // Caching these to prevent repeated copying, do not pass these arrays outside of this class or basic
        // framework methods such as String.IndexOfAny.
        private static readonly char[] invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        private static readonly char[] invalidPathCharacters = Path.GetInvalidPathChars().Concat(new char[] { '*', '?', ':' }).ToArray();
        private static readonly char[] wildcardCharacters = new char[] { '*', '?' };
        private static readonly char[] directorySeparatorCharacters = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private static readonly string[] deviceNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        private static readonly char[] trailingWhitespaceCharacters = new char[] { ' ', '.' };

        /// <summary>
        /// Returns true if the given path contains a wildcard character.
        /// </summary>
        public static bool ContainsWildcard(string path)
        {
            return path.IndexOfAny(PathHelper.wildcardCharacters) != -1;
        }

        /// <summary>
        /// Returns a list of paths that matches given path with wildcard char.
        /// This method assumes the wildcard only appears in file name.
        /// </summary>
        public static IEnumerable<string> ResolveWildcard(string path)
        {
            if (PathHelper.ContainsWildcard(path))
            {
                // Have to be very careful here- wildcard characters aren't "legal" in paths and will cause some System.IO.Path
                // methods to throw (which PathHelper calls in some cases).
                string filename = PathHelper.GetFileOrDirectoryName(path);
                string directory = path.Substring(0, path.Length - filename.Length);
                if (PathHelper.DirectoryExists(directory))
                {
                    foreach (string resolvedPath in PathHelper.EnumerateFiles(directory, filename, SearchOption.TopDirectoryOnly, throwOnError: false))
                    {
                        yield return resolvedPath;
                    }
                }
            }
            else
            {
                if (PathHelper.FileExists(path))
                {
                    yield return path;
                }
            }
        }

        /// <summary>
        /// Possible directory separator characters.  Note that the alternate directory separator is not used in normalized paths.
        /// </summary>
        public static char[] GetDirectorySeparatorCharacters()
        {
            return (char[])PathHelper.directorySeparatorCharacters.Clone();
        }

        /// <summary>
        /// Helper method to determine linked path for a project item
        /// </summary>
        public static string GetLinkedPath(string projectRoot, string path, string linkMetadata)
        {
            // A linked file is specified as follows
            //<Content Include="..\default2.css">
            //	<Link>js\default2.css</Link>
            //</Content>
            // or simply
            //<Content Include="..\default2.css"/>

            // Normal project item with no link metadata
            if (string.IsNullOrEmpty(linkMetadata) && PathHelper.IsPathWithin(path, projectRoot))
            {
                return string.Empty;
            }

            // if link metadata is missing, put the item in project root
            if (string.IsNullOrEmpty(linkMetadata))
            {
                linkMetadata = PathHelper.GetFileOrDirectoryName(path);
            }

            string linkedPath = PathResolver.ResolvePath(Path.Combine(projectRoot, linkMetadata));

            // if returned path is not a valid path or it is not within project root (say link metadata had rooted path C:\foo.css)
            // we want to just put the item at project root
            if (!PathHelper.IsValidPath(linkedPath) || !PathHelper.IsPathWithin(linkedPath, projectRoot))
            {
                linkedPath = Path.Combine(projectRoot, PathHelper.GetFileOrDirectoryName(path));
            }

            return linkedPath;
        }

        /// <summary>
        /// Removes all files within the specified directory.
        /// </summary>
        /// <param name="directoryName">Directory to clean</param>
        /// <param name="deleteTopDirectoryOnError">If any files can't be removed, this indicates whether the top directory
        /// should be removed</param>
        public static void CleanDirectory(string directoryName, bool deleteTopDirectoryOnError)
        {
            if (!PathHelper.DirectoryExists(directoryName))
            {
                return;
            }

            // Clean each directory one at at time.  If any files in a given directory can't be deleted for any reason,
            // we don't delete the subdirectories.
            foreach (string file in AccessHelper.AccessService.DirectoryGetFiles(directoryName))
            {
                try
                {
                    AccessHelper.AccessService.FileSetAttributes(file, FileAttributes.Normal);
                    AccessHelper.AccessService.FileDelete(file);
                }
                catch (IOException e)
                {
                    Debug.WriteLine(string.Format("Failed to delete {0} due to: {1}", file, e.ToString()));
                    deleteTopDirectoryOnError = false;
                    break;
                }
            }

            foreach (string directory in AccessHelper.AccessService.DirectoryGetDirectories(directoryName))
            {
                PathHelper.CleanDirectory(directory, deleteTopDirectoryOnError);
            }

            if (deleteTopDirectoryOnError)
            {
                AccessHelper.AccessService.DirectoryDelete(directoryName, recursive: true);
            }
        }

        /// <summary>
        /// Returns true if the path specified is a directory.
        /// </summary>
        /// <returns>true if the path specified is a directory, false otherwise</returns>
        public static bool IsDirectory(string path)
        {
            if (PathHelper.PathEndsInDirectorySeparator(path))
            {
                return true;
            }

            return AccessHelper.AccessService.MiscDirectoryExists(path);
        }

        /// <summary>
        /// Returns true if the path specified is relative to the current drive or working directory.
        /// Returns false if the path is fixed to a specific drive or UNC path.  This method does no
        /// validation of the path (URIs will be returned as relative as a result).
        /// </summary>
        /// <remarks>
        /// Handles paths that use the alternate directory separator.  It is a frequent mistake to
        /// assume that rooted paths (Path.IsPathRooted) are not relative.  This isn't the case.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        public static bool IsPathRelative(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (path.Length < 2)
            {
                // It isn't fixed, it must be relative.  There is no way to specify a fixed
                // path with one character (or less).
                return true;
            }

            if (PathHelper.IsDirectorySeparator(path[0]))
            {
                // There is no valid way to specify a relative path with two initial slashes
                return !PathHelper.IsDirectorySeparator(path[1]);
            }

            // The only way to specify a fixed path that doesn't begin with two slashes
            // is the drive, colon, slash format- i.e. C:\
            return !(path.Length >= 3
                && path[1] == Path.VolumeSeparatorChar
                && PathHelper.IsDirectorySeparator(path[2]));
        }

        /// <summary>
        /// Combines the given paths, trimming any leading directory seperators from
        /// the start of the second path.
        /// </summary>
        public static string Combine(string path1, string path2)
        {
            path2 = PathHelper.TrimLeadingDirectorySeparators(path2);
            return Path.Combine(path1, path2);
        }

        /// <summary>
        /// Used to pass around path validity status internally.
        /// </summary>
        private enum PathValidity
        {
            ValidPath,
            EmptyPath,
            InvalidServerShare,
            UnknownFormat,
            TrailingSpaceOrPeriod,
            AllDots,
            InvalidPathCharacter,
            DeviceNamePresent
        }

        /// <summary>
        /// Returns true if the specified path is a valid full or relative path.
        /// </summary>
        /// <remarks>
        /// To more closely mimick OS behavior, trim trailing periods and spaces from paths before manipulating.
        /// This returns false for device or extended format paths.
        /// By design, this does not validate the path exists or can be created -- only that it is well-formed
        /// </remarks>
        public static bool IsValidPath(string path)
        {
            return PathHelper.ValidatePathInternal(path) == PathValidity.ValidPath;
        }

        /// <summary>
        /// Validates a path, fixing it if possible by trimming trailing meaningless periods and spaces.
        /// </summary>
        /// <returns>
        /// True if the path is in a valid state.
        /// </returns>
        public static bool TryValidateAndFixPath(string path, out string validatedPath)
        {
            validatedPath = path;

            switch (PathHelper.ValidatePathInternal(path))
            {
                case PathValidity.TrailingSpaceOrPeriod:
                    validatedPath = PathHelper.TrimTrailingPeriodsAndSpaces(path);
                    return PathHelper.ValidatePathInternal(validatedPath) == PathValidity.ValidPath;
                case PathValidity.ValidPath:
                    return true;
                default:
                    return false;
            }
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity",
            Justification = "This is optimized for performance, not necessarily simplicity.")]
        private static PathValidity ValidatePathInternal(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return PathValidity.EmptyPath;
            }

            PathFormat pathFormat = PathHelper.GetPathFormat(path);
            if (pathFormat == PathFormat.UnknownFormat)
            {
                return PathValidity.UnknownFormat;
            }

            // Trying to not create a bunch of strings
            int pathLength = path.Length;
            int currentIndex = 0;
            bool isUnc = false;
            bool isExtended = false;

            // Index past the format specifiers
            switch (pathFormat)
            {
                case PathFormat.DriveAbsolute:
                case PathFormat.DriveRelative:
                    currentIndex = 2;
                    break;
                case PathFormat.DriveAbsoluteExtended:
                    // past the \\?\C:\
                    currentIndex = 7;
                    isExtended = true;
                    break;
                case PathFormat.UniformNamingConventionExtended:
                    // past the \\?\UNC\
                    currentIndex = 8;
                    isUnc = true;
                    isExtended = true;
                    break;
                case PathFormat.UniformNamingConvention:
                    currentIndex = 2;
                    isUnc = true;
                    break;
            }

            if (isExtended)
            {
                return PathValidity.UnknownFormat;
            }

            if (currentIndex == pathLength)
            {
                // If we're a UNC we haven't specified a server\share yet
                if (isUnc)
                {
                    return PathValidity.InvalidServerShare;
                }
                else
                {
                    return PathValidity.ValidPath;
                }
            }

            if (path.IndexOfAny(PathHelper.invalidPathCharacters, currentIndex) != -1)
            {
                // Invalid character present
                return PathValidity.InvalidPathCharacter;
            }

            bool dotsOnly = true;
            int dotCount = 0;
            int segmentCount = 0;
            bool segmentEnd = true;

            // A is always valid so we'll start with this to ensure priorChar checks at the beginning of the string pass
            char priorChar = 'A';

            do
            {
                // Does the current segment start with a device name?
                if (segmentEnd)
                {
                    if (PathHelper.IsDeviceNameInternal(path, currentIndex))
                    {
                        return PathValidity.DeviceNamePresent;
                    }

                    // Reset flags for new name segment
                    segmentEnd = false;
                    dotsOnly = true;
                    dotCount = 0;
                }

                char currentChar = path[currentIndex];
                switch (currentChar)
                {
                    case '/':
                        currentChar = '\\'; // Simplify further checks
                        goto case '\\';
                    case '\\':
                        segmentEnd = true;
                        break;
                    case '.':
                        ++dotCount;
                        break;
                    case ' ':
                    case '=':
                    case ',':
                    case '+':
                    case ';':
                        // Server names have additional constraints
                        // TODO: JKuhne ServerNames also can't have all digits
                        if (isUnc && segmentCount < 1)
                        {
                            return PathValidity.InvalidServerShare;
                        }

                        goto default;
                    default:
                        dotsOnly = false;
                        break;
                }

                ++currentIndex;
                if (currentIndex == pathLength && !segmentEnd)
                {
                    // We've ended here because we don't have a trailing slash. Set priorChar to current to check the segment trailing char correctly.
                    priorChar = currentChar;
                    segmentEnd = true;
                }

                if (segmentEnd)
                {
                    ++segmentCount;

                    if (isUnc)
                    {
                        // Dots aren't NORMALLY valid in server names- Parallels allows this
                        // if ((segmentCount == 1) && (dotCount > 0)) { return PathValidity.InvalidServerShare; }
                        if (segmentCount < 3)
                        {
                            // Share can't be '..' or '.'
                            if (dotsOnly)
                            {
                                return PathValidity.InvalidServerShare;
                            }

                            // Can't have \\\Server\Share or \\Server\\Share.
                            if (currentChar == '\\' && priorChar == '\\')
                            {
                                return PathValidity.InvalidServerShare;
                            }
                        }
                    }

                    // No trailing spaces in a segment
                    if (priorChar == ' ')
                    {
                        return PathValidity.TrailingSpaceOrPeriod;
                    }

                    if (dotsOnly)
                    {
                        if (dotCount > 2)
                        {
                            return PathValidity.AllDots;
                        }
                    }
                    else if (priorChar == '.')
                    {
                        // No trailing dots in a segment
                        return PathValidity.TrailingSpaceOrPeriod;
                    }
                }

                priorChar = currentChar;
            } while (currentIndex < pathLength); // Check for valid names

            if (isUnc && segmentCount < 2)
            {
                // Need a server *and* a share
                return PathValidity.InvalidServerShare;
            }

            return PathValidity.ValidPath;
        }

        /// <summary>
        /// Internal helper for checking for device names.
        /// </summary>
        private static bool IsDeviceNameInternal(string path, int startIndex)
        {
            // Device names are at least 3 chars long
            if (startIndex + 2 >= path.Length)
            {
                return false;
            }

            foreach (string deviceName in PathHelper.deviceNames)
            {
                if (string.Compare(path, startIndex, deviceName, 0, deviceName.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // Starts with a device name
                    for (int i = startIndex + deviceName.Length; i < path.Length; ++i)
                    {
                        char c = path[i];
                        if (PathHelper.IsDirectorySeparator(c) || c == '.')
                        {
                            return true;
                        }

                        if (c != ' ')
                        {
                            // Found something other than a space, dot, or a separator
                            return false;
                        }
                    }

                    // Followed by nothing or nothing but spaces, it's a device name
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the specified name is a reserved device name. ("CON", "LPT3", etc.)
        /// </summary>
        /// <remarks>
        /// This checks the start of the specified name to see if it resolves to a reserved device name.
        /// If the name starts with a directory separator it will return false.  It does no validation
        /// of invalid filename characters.
        /// </remarks>
        public static bool IsDeviceName(string name)
        {
            if (name == null || name.Length < 3)
            {
                return false;
            }

            return PathHelper.IsDeviceNameInternal(name, startIndex: 0);
        }

        /// <summary>
        /// Used to pass around name validity internally.
        /// </summary>
        private enum FileNameValidity
        {
            ValidFileName,
            EmptyName,
            TrailingSpaceOrPeriod,
            DeviceNamePresent,
            InvalidCharacters
        }

        /// <summary>
        /// Returns true if the specified name is a valid file or directory name.
        /// </summary>
        /// <remarks>
        /// While '.' and '..' are valid for path segments, they are not valid directory names.
        /// </remarks>
        public static bool IsValidFileOrDirectoryName(string name)
        {
            return PathHelper.ValidateFileOrDirectoryNameInternal(name) == FileNameValidity.ValidFileName;
        }

        /// <summary>
        /// Returns the index of the extension for the given path.  Does not validate paths in any way.
        /// </summary>
        /// <returns>The index of the period</returns>
        private static int FindExtensionOffset(string pathOrFileName)
        {
            if (string.IsNullOrEmpty(pathOrFileName))
            {
                return -1;
            }

            int length = pathOrFileName.Length;

            // If are only one character long or we end with a period, return
            if ((length == 1) || pathOrFileName[length - 1] == '.')
            {
                return -1;
            }

            // Walk the string backwards looking for a period
            int index = length;
            while (--index >= 0)
            {
                char ch = pathOrFileName[index];
                if (ch == '.')
                {
                    return index;
                }

                if (PathHelper.IsDirectorySeparator(ch) || ch == Path.VolumeSeparatorChar)
                {
                    // Found a directory or volume separator before a period
                    return -1;
                }
            }

            // No period at all
            return -1;
        }

        /// <summary>
        /// Trims the extension, if any, from the given path or file name. Does not throw.
        /// </summary>
        public static string TrimExtension(string pathOrFileName)
        {
            int extensionIndex = PathHelper.FindExtensionOffset(pathOrFileName);
            if (extensionIndex == -1)
            {
                // Nothing valid to trim, do nothing
                return pathOrFileName;
            }
            else
            {
                return pathOrFileName.Substring(0, extensionIndex);
            }
        }

        /// <summary>
        /// Attempt to retreive a file extension (with period), if any, from the given path or file name. Does not throw.
        /// </summary>
        public static string GetExtension(string pathOrFileName)
        {
            int extensionIndex = PathHelper.FindExtensionOffset(pathOrFileName);
            if (extensionIndex == -1)
            {
                // Nothing valid- return nothing
                return string.Empty;
            }
            else
            {
                return pathOrFileName.Substring(extensionIndex);
            }
        }

        private static FileNameValidity ValidateFileOrDirectoryNameInternal(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return FileNameValidity.EmptyName;
            }

            if (name.IndexOfAny(PathHelper.invalidFileNameCharacters) != -1)
            {
                return FileNameValidity.InvalidCharacters;
            }

            if (PathHelper.IsDeviceName(name))
            {
                return FileNameValidity.DeviceNamePresent;
            }

            switch (name[name.Length - 1])
            {
                case ' ':
                case '.':
                    return FileNameValidity.TrailingSpaceOrPeriod;
                default:
                    return FileNameValidity.ValidFileName;
            }
        }

        /// <summary>
        /// Gets the format of the specified path.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        public static PathFormat GetPathFormat(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (path.Length == 0)
            {
                return PathFormat.UnknownFormat;
            }

            char firstChar = path[0];
            if (!PathHelper.IsDirectorySeparator(firstChar))
            {
                // Path does not start with a slash
                if (path.Length < 2 || path[1] != Path.VolumeSeparatorChar)
                {
                    return PathFormat.Relative;
                }

                // We've got a colon, check the drive
                char drive = char.ToUpperInvariant(firstChar);
                if (drive < 'A' || drive > 'Z')
                {
                    // Not a valid drive identifier
                    return PathFormat.UnknownFormat;
                }

                if (path.Length < 3 || !PathHelper.IsDirectorySeparator(path[2]))
                {
                    return PathFormat.DriveRelative;
                }

                return PathFormat.DriveAbsolute;
            }
            else
            {
                // Path starts with a slash
                if (path.Length < 2 || !PathHelper.IsDirectorySeparator(path[1]))
                {
                    // Starts with just a single backslash
                    return PathFormat.Rooted;
                }

                if (path.Length < 3)
                {
                    return PathFormat.UnknownFormat;
                }

                // Starts with a double slash, check for the special chars
                if ((path[2] == '?')
                    ||
                    (
                        // PS 102649- Parallels uses \\.psf\Home\ for their desktop sharing feature
                        (path[2] == '.')
                        &&
                        (
                            (path.Length == 3)  // Just '\\.' (Rooted local device)
                            || (path[3] == '\\')  // '\\.\' (Local device) (we're 4 or more chars given the < 3 above)
                        )
                    ))
                {
                    // Extended format or local device type
                }
                else
                {
                    return PathFormat.UniformNamingConvention;
                }

                // Now we've got a special char, check for the valid extended formats
                if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
                {
                    return PathFormat.UniformNamingConventionExtended;
                }

                if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
                {
                    if ((path.Length >= 7) && (path[5] == ':') && (path[6] == '\\'))
                    {
                        char drive = char.ToUpperInvariant(path[4]);
                        if (drive >= 'A' && drive <= 'Z')
                        {
                            return PathFormat.DriveAbsoluteExtended;
                        }
                    }
                }
            }

            return PathFormat.UnknownFormat;
        }

        /// <summary>
        /// Returns a normalized path. This method is expensive.
        /// </summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            string resolvedPath = PathResolver.ResolvePath(path);
            return PathHelper.TrimTrailingDirectorySeparators(resolvedPath);
        }

        /// <summary>
        /// Compares the canonical form of the two specified paths.  Returns true if the OS would consider them equal.
        /// </summary>
        public static bool ArePathsEquivalent(string firstPath, string secondPath)
        {
            if (firstPath == null || secondPath == null)
            {
                return secondPath == null && firstPath == null;
            }

            return string.Equals(
                PathHelper.TrimTrailingDirectorySeparators(firstPath),
                PathHelper.TrimTrailingDirectorySeparators(secondPath),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The string returned by this consists of all characters in the path up to *and* including the last DirectorySeparatorChar or AltDirectorySeparatorChar.
        /// </summary>
        /// <returns>The directory or null if the path is invalid.</returns>
        public static string GetDirectoryNameOrRoot(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            PathFormat pathFormat = PathHelper.GetPathFormat(fullPath);
            if (pathFormat == PathFormat.UnknownFormat)
            {
                return null;
            }

            if (!PathHelper.IsUncJustServerShare(pathFormat, fullPath))
            {
                // "Normal" path- find the last trailing slash (past any possible \\ for a unc)
                // (directly copied from the framework code for Path.GetDirectoryName)
                int length = fullPath.Length;
                while (length > 2
                    && fullPath[--length] != Path.DirectorySeparatorChar
                    && fullPath[length] != Path.AltDirectorySeparatorChar)
                {
                }

                fullPath = fullPath.Substring(0, length);
            }

            string directory = PathHelper.EnsurePathEndsInDirectorySeparator(fullPath);
            return PathHelper.IsValidPath(directory) ? directory : null;
        }

        /// <summary>
        /// Returns true if the path begins with a directory separator.
        /// </summary>
        public static bool PathBeginsWithDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return PathHelper.IsDirectorySeparator(path[0]);
        }

        /// <summary>
        /// Returns true if the path ends in a directory separator.
        /// </summary>
        public static bool PathEndsInDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            char lastChar = path[path.Length - 1];
            return PathHelper.IsDirectorySeparator(lastChar);
        }

        public static bool IsDirectorySeparator(char character)
        {
            return character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Ensures that the specified path begins with a directory separator.
        /// </summary>
        /// <returns>The path with an prepended directory separator if necessary.</returns>
        public static string EnsurePathBeginsWithDirectorySeparator(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (PathHelper.PathBeginsWithDirectorySeparator(path) || Path.IsPathRooted(path))
            {
                return path;
            }
            else
            {
                return Path.DirectorySeparatorChar + path;
            }
        }

        /// <summary>
        /// Ensures that the specified path ends in a directory separator.
        /// </summary>
        /// <returns>The path with an appended directory separator if necessary.</returns>
        public static string EnsurePathEndsInDirectorySeparator(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (PathHelper.PathEndsInDirectorySeparator(path))
            {
                return path;
            }
            else
            {
                return path + Path.DirectorySeparatorChar;
            }
        }

        /// <summary>
        /// Trims known directory separators from the end of a path.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        public static string TrimTrailingDirectorySeparators(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return path.TrimEnd(PathHelper.directorySeparatorCharacters);
        }

        /// <summary>
        /// Trims known directory separators from the beginning of a path.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        public static string TrimLeadingDirectorySeparators(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return path.TrimStart(PathHelper.directorySeparatorCharacters);
        }

        /// <summary>
        /// Trims trailing periods and spaces (please see remarks)
        /// </summary>
        /// <remarks>
        /// Be cautious with this.  The intent here is to clean up paths that are coming
        /// from external sources that end in segments that have trailing periods.  You'll
        /// want to clean things like "C:\Foo    " or "C:\Foo.", but not "C:\Foo\..".
        /// </remarks>
        private static string TrimTrailingPeriodsAndSpaces(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return path.TrimEnd(PathHelper.trailingWhitespaceCharacters);
        }

        /// <summary>
        /// Replaces the DirectorySeparatorChar with "/" for web paths.
        /// </summary>
        public static string ChangePathToWebFormat(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return path.Replace(Path.DirectorySeparatorChar, '/');
        }

        /// <summary>
        /// Replaces the "/" for DirectorySeparatorChar. 
        /// </summary>
        public static string ChangePathToLocalPathFormat(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Given a desired name and extension, will find an available file or directory name.
        /// </summary>
        /// <param name="directory">The resolved path of the directory to find an available name in.</param>
        /// <param name="desiredExtension">The desired extension, if any. Can be null.</param>
        /// <param name="alwaysAppendDigit">Set to true to always add digits to the end of the name.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the desiredNameWithoutExtension or directory is null.</exception>
        /// <exception cref="System.Security.SecurityException">Thrown if the caller doesn't have IO permission.</exception>
        /// <returns>The available name, or null if it cannot find a name.</returns>
        public static string GetAvailableFileOrDirectoryName(string desiredNameWithoutExtension, string desiredExtension, string directory, bool alwaysAppendDigit)
        {
            if (desiredNameWithoutExtension == null)
            {
                throw new ArgumentNullException("desiredNameWithoutExtension");
            }

            if (directory == null)
            {
                throw new ArgumentNullException("directory");
            }

            if (!PathHelper.IsValidPath(directory))
            {
                return null;
            }

            string extension = string.Empty;
            if (!string.IsNullOrEmpty(desiredExtension))
            {
                if (desiredExtension[0] != '.')
                {
                    extension = "." + desiredExtension;
                }
                else
                {
                    extension = desiredExtension;
                }
            }

            // bugfix 878825: '{' '}' are valid characters for a path
            // before creating the pathFormat string, we should escape these characters
            // For e.g. pathFormat = C:\\foo{\\{0}
            // Sting.format API will throw a format exception with above as format string
            string resolvedDirectoryPath = directory.Replace("{", "{{").Replace("}", "}}");
            string pathFormat = Path.Combine(resolvedDirectoryPath, desiredNameWithoutExtension + @"{0}" + extension);

            string modifier = PathHelper.GetAvailablePathModifier(pathFormat, alwaysAppendDigit);
            return desiredNameWithoutExtension + modifier + extension;
        }

        /// <summary>
        /// Gets a modifier to the given a path format that makes it unique in its directory.
        /// </summary>
        /// <param name="pathFormat">String that include a {0} where digits are intended to be added</param>
        /// <param name="alwaysUseDigit">If true always tries to insert a digit, if false, tries to insert nothing before inserting digits</param>
        /// <exception cref="System.ArgumentNullException">Thrown if pathFormatStrings is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if start digit is less than 0.</exception>
        /// <exception cref="System.Security.SecurityException">Thrown if the caller doesn't have IO permission.</exception>
        /// <exception cref="System.FormatException">Thrown if given invalid format strings.</exception>
        public static string GetAvailablePathModifier(string pathFormat, bool alwaysUseDigit)
        {
            if (pathFormat == null)
            {
                throw new ArgumentNullException("pathFormat");
            }

            if (pathFormat == string.Format(CultureInfo.InvariantCulture, pathFormat, "1"))
            {
                // We want to avoid processing inputs that dont change with a format argument
                // (should be "Foo{0}.bar", not "Foo.bar")
                throw new FormatException("Format strings evaluate to input with format argument.");
            }

            if (!alwaysUseDigit)
            {
                if (PathHelper.IsFormattedPathAvailable(pathFormat, string.Empty))
                {
                    // We're already good, no modifier is necessary
                    return string.Empty;
                }
            }

            const int MaxModifier = 10000;

            // Start appending digits until we find a valid path
            int modifier = 1;
            while (!PathHelper.IsFormattedPathAvailable(pathFormat, modifier.ToString(CultureInfo.InvariantCulture)))
            {
                ++modifier;

                // Give up if we've gone too far
                if (modifier > MaxModifier)
                {
                    Debug.Fail("We were unable to find a usable path- this may be a result of bad input.");
                    return string.Empty;
                }
            }

            return modifier.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsFormattedPathAvailable(string pathFormat, string formatArgument)
        {
            string path = string.Format(CultureInfo.InvariantCulture, pathFormat, formatArgument);
            return !PathHelper.FileOrDirectoryExists(path);
        }

        /// <summary>
        /// Returns the file or directory name for the specified path.
        /// </summary>
        /// <remarks>
        /// Does not validate path characters or syntax (as System.IO.Path will).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        public static string GetFileOrDirectoryName(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            // Directory paths can be specified with a trailing slash, ensuring this
            // is removed gets us the directory name
            path = PathHelper.TrimTrailingDirectorySeparators(path);
            if (path.Length == 0)
            {
                return path;
            };

            int length = path.Length;
            int startIndex = length;
            while (--startIndex >= 0)
            {
                char ch = path[startIndex];
                if (PathHelper.IsDirectorySeparator(ch) || ch == Path.VolumeSeparatorChar)
                {
                    // Found a separator, return from here
                    return path.Substring(startIndex + 1);
                }
            }

            // Didn't find any separator, return as is
            return path;
        }

        /// <summary>
        /// Returns the file or directory name for the specified path.
        /// </summary>
        /// <remarks>
        /// Does not validate path characters or syntax (as System.IO.Path will).
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        public static string GetFileOrDirectoryNameWithoutExtension(string path)
        {
            return PathHelper.TrimExtension(PathHelper.GetFileOrDirectoryName(path));
        }

        /// <summary>
        /// Returns true if the specified UNC is just the server and share specified
        /// MUST be valid UNC
        /// </summary>
        public static bool IsUncJustServerShare(PathFormat pathFormat, string unc)
        {
            if (pathFormat != PathFormat.UniformNamingConvention)
            {
                return false;
            }

            // Skip the \\ and look to see if there are any separators past the server\share separator
            // if not, or just trailing another separator, it's just a server\share, 
            int separatorPosition = unc.IndexOfAny(PathHelper.directorySeparatorCharacters, 2);
            separatorPosition = unc.IndexOfAny(PathHelper.directorySeparatorCharacters, ++separatorPosition);
            return (separatorPosition == -1 || separatorPosition == unc.Length - 1);
        }

        /// <summary>
        /// Returns the directory for the given path.
        /// </summary>
        /// <remarks>
        /// A path is determined to be a directory if (1) the directory exists, or (2) the path ends in a directory separator.
        /// 
        /// If the path is NOT absolute (not C:\ or \\Server\Share) it will attempt to find the directory without resolving
        /// to an absolute path.  If unsuccessful it will return an empty string.  If you want resolution against the resolved
        /// path, call ResolvePath() or ResolveRelativePath().
        /// </remarks>
        /// <returns>Directory WITH a trailing separator or an empty string if the path is relative.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the path is an invalid format.</exception>
        public static string GetDirectory(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            PathFormat pathFormat = PathHelper.GetPathFormat(path);
            if (pathFormat == PathFormat.UnknownFormat)
            {
                throw new ArgumentOutOfRangeException("path",
                    string.Format(CultureInfo.InvariantCulture, "Invalid path format: '{0}'", path));
            }

            // If a \\Server\Share without a path (ie, no \\Server\Share\Path) return as is
            if (PathHelper.IsUncJustServerShare(pathFormat, path))
            {
                return PathHelper.EnsurePathEndsInDirectorySeparator(path);
            }

            if (PathHelper.IsDirectory(path))
            {
                return PathHelper.EnsurePathEndsInDirectorySeparator(path);
            }

            // Path.GetDirectoryName retuns null for root paths or paths that are shorter than the root,
            // it then walks the string backwards until it finds a directory separator
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                return PathHelper.EnsurePathEndsInDirectorySeparator(directory);
            }
            else
            {
                // Cannot discern the directory for the given path
                return String.Empty;
            }
        }

        /// <summary>
        /// Returns the parent directory of the the specified path.
        /// (The directory the file or directory resides in.)
        /// </summary>
        /// <remarks>
        /// A path is determined to be a directory if (1) the directory exists, or (2) the path ends in a directory separator.
        /// 
        /// If the path is NOT absolute (not C:\ or \\Server\Share) it will attempt to find the parent directory without resolving
        /// to an absolute path.  If unsuccessful it will return an empty string.  If you want resolution against the resolved
        /// path, call ResolvePath() or ResolveRelativePath().
        /// </remarks>
        /// <returns>Directory WITH a trailing separator.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if path is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the path is an invalid format.</exception>
        public static string GetParentDirectory(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            PathFormat pathFormat = PathHelper.GetPathFormat(path);
            if (pathFormat == PathFormat.UnknownFormat)
            {
                throw new ArgumentOutOfRangeException("path",
                    string.Format(CultureInfo.InvariantCulture, "Invalid path format: '{0}'", path));
            }

            // If a \\Server\Share without a path (ie, no \\Server\Share\Path) return as is
            if (PathHelper.IsUncJustServerShare(pathFormat, path))
            {
                return PathHelper.EnsurePathEndsInDirectorySeparator(path);
            }

            // Path.GetDirectoryName retuns null for root paths or paths that are shorter than the root,
            // it then walks the string backwards until it finds a directory separator
            if (PathHelper.IsDirectory(path))
            {
                // We've got a directory
                string trimmedPath = PathHelper.TrimTrailingDirectorySeparators(path);
                if (trimmedPath.Length == 0)
                {
                    return string.Empty;
                }

                string directory = Path.GetDirectoryName(trimmedPath);
                if (string.IsNullOrEmpty(directory))
                {
                    return string.Empty;
                }
                else
                {
                    return PathHelper.EnsurePathEndsInDirectorySeparator(directory);
                }
            }
            else
            {
                // We've got a file
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    // Can't walk any higher
                    return string.Empty;
                }
                else
                {
                    return PathHelper.EnsurePathEndsInDirectorySeparator(directory);
                }
            }
        }

        // The following exists methods are much faster than the framework methods (File.Exists & Directory.Exists) as these methods
        // do not normalize the path and demand read FileIOPermission.

        /// <summary>
        /// Returns true if the specified path exists or is invalid.  Significantly faster than calling File.Exists() || Directory.Exists().
        /// </summary>
        /// <exception cref="System.UnauthorizedAccessException">Thrown if access to the specified path is denied.</exception>
        public static bool FileOrDirectoryExists(string path)
        {
            if (path == null)
            {
                return false;
            }

            // No need to normalize (costly). The Windows API does this as well as handling null, empty paths, invalid chars, etc.
            return AccessHelper.AccessService.MiscPathExists(path);
        }

        /// <summary>
        /// Returns true if the specified file exists.  Does not return true for directories or invalid paths.
        /// </summary>
        /// <exception cref="System.UnauthorizedAccessException">Thrown if access to the specified path is denied.</exception>
        public static bool FileExists(string path)
        {
            if (path == null)
            {
                return false;
            }

            return AccessHelper.AccessService.MiscFileExists(path);
        }

        /// <summary>
        /// Returns true if the specified directory exists.  Does not return true for files or invalid paths.
        /// </summary>
        /// <param name="throwOnError">Returns false instead of throwing if set to true</param>
        /// <exception cref="System.UnauthorizedAccessException">Thrown if access to the specified path is denied.</exception>
        public static bool DirectoryExists(string path, bool throwOnError = true)
        {
            if (path == null)
            {
                return false;
            }

            try
            {
                return AccessHelper.AccessService.MiscDirectoryExists(path);
            }
            catch (UnauthorizedAccessException)
            {
                if (throwOnError)
                {
                    throw;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns a list of files in given directory matches given filename with wildcard char.
        /// </summary>
        /// <param name="throwOnError">Returns empty enumerable instead of throwing if set to true</param>
        public static IEnumerable<string> EnumerateFiles(string directory, string fileName, SearchOption searchOption = SearchOption.AllDirectories, bool throwOnError = true)
        {
            IEnumerable<string> files = Enumerable.Empty<string>();
            if (directory != null && fileName != null)
            {
                if (!throwOnError)
                {
                    ErrorHandling.HandleBasicExceptions(
                        action: () => files = AccessHelper.AccessService.DirectoryEnumerateFiles(directory, fileName, searchOption),
                        exceptionHandlers: ErrorHandling.BasicIOExceptionHandler);
                }
                else
                {
                    files = AccessHelper.AccessService.DirectoryEnumerateFiles(directory, fileName, searchOption);
                }
            }

            return files;
        }

        /// <summary>
        /// Clears the read only flag for the specified path.
        /// </summary>
        public static bool ClearFileOrDirectoryReadOnlyAttribute(string path)
        {
            FileAttributes attributes;
            if (PathHelper.IsFileOrDirectoryReadOnly(path, out attributes))
            {
                if (!AccessHelper.AccessService.MiscSetFileAttributes(path, attributes & ~FileAttributes.ReadOnly))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get attributes to the specified path.
        /// </summary>
        /// <param name="useAccessService">Set to false to skip using the access service- useful for determining whether or not access is denied.</param>
        /// <exception cref="System.UnauthorizedAccessException">Thrown if access to the specified path is denied.</exception>
        /// <returns>"false" if failed to get attributes (path does not exist, bad path, etc.)</returns>
        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters",
            Justification = "Not worth the extra complication as attributes is an Enum we can't return null here to indicate failure. Having attributes be the out helps enforce the fact that this can fail and have bad data.")]
        public static bool GetPathAttributes(string path, out FileAttributes attributes, bool useAccessService = true)
        {
            if (useAccessService)
            {
                return AccessHelper.AccessService.MiscGetFileAttributes(path, out attributes);
            }
            else
            {
                return Interop.NativeMethods.GetFileAttributes(path, out attributes);
            }
        }

        private static bool IsFileOrDirectoryReadOnly(string path, out FileAttributes attributes)
        {
            // No need to normalize (costly).  The Windows API does this as well as handling null, empty paths, invalid chars, etc.
            if (!AccessHelper.AccessService.MiscGetFileAttributes(path, out attributes))
            {
                return false;
            }

            return (attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
        }

        /// <summary>
        /// Checks if firstpath location is inside secondpath location
        /// For e.g. 
        /// a/b/c/d is inside a/b 
        /// e/f/c/d is not inside a/b 
        /// </summary>
        /// <returns>returns true if firstpath lies inside second path </returns>
        public static bool IsPathWithin(string firstPath, string secondPath)
        {
            if (firstPath != null
                && secondPath != null
                && PathHelper.IsValidPath(firstPath)
                && PathHelper.IsValidPath(secondPath))
            {
                // Make sure the paths end in terminators so that the substring checks will work.
                // We need to make sure that the second path is terminated in a separator to make sure that the file "food" does not appear to be in the "foo" directory.
                // Consequently, we also need to terminate the first path to handle the case where the first and second paths are equal.
                // It's OK if we wind up putting a terminator at the end of a file, since we are just doing a substring check.
                string firstPathTerminated = PathHelper.EnsurePathEndsInDirectorySeparator(firstPath);
                string secondPathTerminated = PathHelper.EnsurePathEndsInDirectorySeparator(secondPath);

                return firstPathTerminated.StartsWith(secondPathTerminated, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if both locations are under the same directory.
        /// </summary>
        public static bool ArePathsWithinSameDirectory(string firstPath, string secondPath)
        {
            return PathHelper.ArePathsEquivalent(PathHelper.GetParentDirectory(firstPath), PathHelper.GetParentDirectory(secondPath));
        }

        /// <summary>
        /// Converts absolute path from one location to another.
        /// </summary>
        /// <param name="pathInSource">The source path.</param>
        /// <param name="sourceDir">The "root" directory path of the source path.</param>
        /// <param name="targetDir">The new "root" directory path.</param>
        /// <returns>The new path, or null if unable to compute the new path.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if any argument is null.</exception>
        public static string ConvertSourceToTarget(string pathInSource, string sourceDir, string targetDir)
        {
            if (pathInSource == null)
            {
                throw new ArgumentNullException("pathInSource");
            }

            if (sourceDir == null)
            {
                throw new ArgumentNullException("sourceDir");
            }

            if (targetDir == null)
            {
                throw new ArgumentNullException("targetDir");
            }

            // If path is the same as source dir then we should return null, e.g.
            //   source dir:  c:\foo\bar\
            //   path:        c:\foo\bar\
            // Also verify that path starts with source dir
            if (sourceDir.Length >= pathInSource.Length || !PathHelper.IsPathWithin(pathInSource, sourceDir))
            {
                return null;
            }

            string relativePath = null;
            // Make sure that the point where source dir ends is a directory
            // separator. We want to avoid false positive for a case like:
            //   source dir:  c:\foo\bar   (could be a dir or a file)
            //   path:        c:\foo\bar123.txt
            // If source dir ends with '\' or '//' then everything is OK, e.g.
            //   source dir:  c:\foo\bar\
            //   path:        c:\foo\bar\123.txt
            if (!PathHelper.PathEndsInDirectorySeparator(sourceDir))
            {
                if (!PathHelper.IsDirectorySeparator(pathInSource[sourceDir.Length]))
                {
                    // This is the case of
                    //   source dir:  c:\foo\bar   (could be a dir or a file)
                    //   path:        c:\foo\bar123.txt
                    return null;
                }

                if (sourceDir.Length + 1 >= pathInSource.Length)
                {
                    // This is the case of
                    //   source dir:  c:\foo\bar
                    //   path:        c:\foo\bar\
                    return null;
                }

                relativePath = pathInSource.Substring(sourceDir.Length + 1);
            }
            else
            {
                relativePath = pathInSource.Substring(sourceDir.Length);
            }

            return Path.Combine(targetDir, relativePath);
        }

        /// <summary>
        /// Gives the relative path from one location to another.
        /// </summary>
        /// <returns>The relative path or null if either path is invalid. If the paths are the same, returns the toPath.</returns>
        public static string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath) || string.IsNullOrEmpty(toPath))
            {
                return toPath;
            }

            if (string.Equals(fromPath, toPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // We leverage the fact that DocumentReference is a normalized path (no current or parent directory periods
            // or double periods). This allows us to make a number of optimizations. We try to avoid over comparing and
            // push normalizing case as late as possible. Basic steps:
            //
            //  1. Find if the root "drive" (or UNC share) is common between the two paths
            //  2. If it is find the topmost common directory
            //  3. Build the relative path from the common directory
            string fromDirectory = PathHelper.GetDirectory(fromPath);

            // If the strings are precisely the same, get out
            if (object.ReferenceEquals(fromDirectory, toPath))
            {
                return string.Empty;
            }

            // A fully qualified path will be either a UNC (Server\Share) or a drive letter rooted path (C:). If the
            // "root" is different the full to path is the "relative" path
            int rootSeparator = 0;

            // Compare the roots
            if (fromDirectory[1] == ':')
            {
                // From a local path
                if (toPath[1] != ':')
                {
                    // ToPath is a UNC, can't make relative
                    Debug.Assert(toPath.StartsWith(@"\\"), "The toPath should be a UNC");
                    return toPath;
                }
                else if (char.ToLowerInvariant(fromDirectory[0]) != char.ToLowerInvariant(toPath[0]))
                {
                    // Different drives, can't make relative
                    return toPath;
                }

                rootSeparator = 2; // The postion of the '\' after the drive colon
            }
            else
            {
                // From a UNC, find where the \\Server\Share\ specification ends and compare
                // (first two characters MUST be back slashes)
                Debug.Assert(fromDirectory.StartsWith(@"\\"), "The from directory should be a UNC");

                if (toPath[1] == Path.VolumeSeparatorChar)
                {
                    // ToPath is local, can't make relative
                    return toPath;
                }

                int serverShareSeparator = fromDirectory.IndexOfAny(PathHelper.directorySeparatorCharacters, 2);
                rootSeparator = fromDirectory.IndexOfAny(PathHelper.directorySeparatorCharacters, serverShareSeparator + 1);

                // Early out here if the separator isn't in the same spot
                if (toPath.Length <= rootSeparator || fromDirectory[rootSeparator] != toPath[rootSeparator])
                {
                    return toPath;
                }
            }

            // Normalize the paths for case- this will also trim any trailing separators
            string normalizedFromDirectory = PathHelper.TrimTrailingDirectorySeparators(fromDirectory).ToUpperInvariant();
            string normalizedToPath = PathHelper.TrimTrailingDirectorySeparators(toPath).ToUpperInvariant();

            if (rootSeparator != 2)
            {
                // Not a local path, still need to validate \\Server\Share are equivalent
                for (int i = 2; i < rootSeparator; ++i)
                {
                    if (normalizedFromDirectory[i] != normalizedToPath[i])
                    {
                        // Different server\share
                        return toPath;
                    }
                }
            }

            // We are now sure that we have a shared root- find the shared start of the paths
            int lastCommonSeparator = rootSeparator;
            int finalSharedCharacter = rootSeparator;

            for (int i = lastCommonSeparator; i < normalizedFromDirectory.Length && i < normalizedToPath.Length; ++i)
            {
                char fromChar = normalizedFromDirectory[i];
                char toChar = normalizedToPath[i];

                if (PathHelper.IsDirectorySeparator(fromChar) && PathHelper.IsDirectorySeparator(toChar))
                {
                    lastCommonSeparator = i;
                    finalSharedCharacter = i;
                    continue;
                }

                if (fromChar == toChar)
                {
                    finalSharedCharacter = i;
                    continue;
                }

                // Not equivalent, found the end of the shared start
                break;
            }

            // FromPath = @"L:\Foo\", ToPath = @"L:\Foo", ExpectedResult = @"..\Foo"
            // Is the toPath the fromDirectory?
            if (normalizedToPath.Length - 1 == finalSharedCharacter
                && normalizedFromDirectory.Length == normalizedToPath.Length
                && PathHelper.IsDirectorySeparator(fromDirectory[finalSharedCharacter + 1]))
            {
                return ".." + toPath.Substring(lastCommonSeparator);
            }

            // Since we've trimmed out the last trailing slash as a part of normalization, if the next unshared character
            // in the TO path is a separator, we were at a shared segment - but only if we are at the end of the FROM path.  
            // (E.G. C:\Foo => C:\Foo\Bar.txt)
            // DevDiv2 397970. If FROM path contains TO path, the last shared character will be the final character of TO path,
            // We do not need to scan the next character. Otherwise, it will throw IndexOutOfRange Exception
            // in normalizedToPath[finalSharedCharacter+1]. (E.G. C:\Foo\Bar => C:\Foo).
            if (normalizedToPath.Length - 1 != finalSharedCharacter
                && normalizedFromDirectory.Length - 1 == finalSharedCharacter
                && PathHelper.IsDirectorySeparator(normalizedToPath[finalSharedCharacter + 1]))
            {
                lastCommonSeparator = finalSharedCharacter + 1;
            }

            // Now we need to count the remaining directory segments in the from path to dot ourselves back to the shared root
            int afterLastCommonSeparator = lastCommonSeparator + 1;
            int parentDirectoryCount = 0;

            if (afterLastCommonSeparator < normalizedFromDirectory.Length)
            {
                parentDirectoryCount++;
                for (int i = afterLastCommonSeparator; i < normalizedFromDirectory.Length; ++i)
                {
                    char fromChar = normalizedFromDirectory[i];
                    if (PathHelper.IsDirectorySeparator(fromChar))
                    {
                        parentDirectoryCount++;
                    }
                }
            }

            if (parentDirectoryCount == 0)
            {
                // No need move up directories
                return toPath.Substring(lastCommonSeparator + 1);
            }
            else
            {
                StringBuilder sb = new StringBuilder(parentDirectoryCount * 3 + toPath.Length - lastCommonSeparator);
                for (int i = 0; i < parentDirectoryCount; ++i)
                {
                    sb.Append(@"..\");
                }

                sb.Append(toPath.Substring(lastCommonSeparator + 1));
                return sb.ToString();
            }
        }

        public static bool HasExtension(string path, params string[] extensions)
        {
            string pathExtension = PathHelper.GetExtension(path);
            return extensions.Any(extension => string.Equals(pathExtension, extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryRepeatedFileCopy(string source, string destination, bool overwrite, int timesToTry, TimeSpan timeout)
        {
            bool success = true;
            for (int timesTried = 0; timesTried <= timesToTry; timesTried++)
            {
                Exception exception = null;
                try
                {
                    File.Copy(source, destination, overwrite);
                    PathHelper.ClearFileOrDirectoryReadOnlyAttribute(destination);
                }
                catch (UnauthorizedAccessException e)
                {
                    exception = e;
                }
                catch (IOException e)
                {
                    exception = e;
                }

                success = exception == null;
                if (!success)
                {
                    Debug.WriteLine("File.Copy try {0} out of {1} failed due to {2}. Waiting {3} milliseconds...", timesTried + 1, timesToTry, exception.ToString(), timeout.TotalMilliseconds);
                    Thread.Sleep((int)timeout.TotalMilliseconds);
                }
                else
                {
                    return true;
                }
            }
#if DEBUG
            if (File.Exists(destination))
            {
                Debug.WriteLine(string.Format("Failed to copy {0} to {1} as {1} already exists and is likely in use.", source, destination));
            }
            else
            {
                DebugHelper.Fail(string.Format("File.Copy {0} to {1} failed after {2} attempts!", source, destination, timesToTry));
            }
#endif
            return success;
        }

        /// <summary>For Foo\Bar\Random.jpg return Foo. Used primarily for NuGet package name extraction</summary>
        public static string GetFirstDirectory(string relativePath)
        {
            int index = relativePath.IndexOf('\\');
            if (index != -1)
            {
                return relativePath.Substring(0, index);
            }
            return string.Empty;
        }
    }
}
