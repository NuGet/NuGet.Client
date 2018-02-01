using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace NuGetClient.Test.Foundation.Utility.Interop
{
    internal delegate bool EnumWindowsCB(IntPtr hwnd, IntPtr lParam);
    internal static class NativeMethods
    {
        // Important Interop Links
        // =======================
        //
        // "Windows Data Types"                  https://msdn.microsoft.com/en-us/library/aa383751.aspx
        // "Windows Data Types for Strings"      https://msdn.microsoft.com/en-us/library/dd374131.aspx
        // "Data Type Ranges"                    https://msdn.microsoft.com/en-us/library/s3f49ktz.aspx
        // "MarshalAs Attribute"                 https://msdn.microsoft.com/en-us/library/system.runtime.interopservices.marshalasattribute.aspx
        // "Marshalling between Managed and Unmanaged Code"
        //                                       https://msdn.microsoft.com/en-us/magazine/cc164193.aspx

        // In/Out attributes implicitly applied for parameter & return values
        //
        // None Specified -> [In]
        // out            -> [Out]
        // ref            -> [In],[Out]
        // return value   -> [Out]

        // [PreserveSig(false)]
        // When this is explicitly set to false (the default is true), failed HRESULT return values will be turned into Exceptions
        // (and the return value in the definition becomes null as a result)

        // Strings:
        // ========
        //
        // Specifying [DllImport(CharSet = CharSet.Unicode)] or [MarshalAs(UnmanagedType.LPWSTR)] will allow strings to be pinned,
        // which improves interop performance.
        //
        // The CLR will always use CoTaskMemFree to free strings that are passed as [Out] or SysStringFree for strings that are marked
        // as BSTR.
        //
        // (StringBuilder)
        // By default it is passed as [In, Out]. Do NOT use 'out' or 'ref' as this will degrade perf (the CLR cannot pin the internal
        // buffer). ALWAYS specify the capacity in advance and ensure it is large enough for API in question. Do NOT use for ANSI.

        // TODO: As code is moved out of the conditionals then remove it from the Framework\NativeMethods.cs (if possible)
        // TODO: When the breakup is complete remove the code remaining in the conditionals (it's only here to make it easy to keep things in the same order while doing the migration).
        // TODO: When the move is complete, make a pass thru the Framework\NativeMethods to look for anything that is unused but (because of the incremental moves) ended up in both places.
        internal static IntPtr NO_PARENT = IntPtr.Zero;

        internal static int S_OK = 0;

        // Common Item Dialog documentation (IFileDialog, IFileOpenDialog, IFileSaveDialog)
        // https://msdn.microsoft.com/en-us/library/windows/desktop/bb776913.aspx
        [Flags]
        internal enum FOS : uint
        {
            FOS_OVERWRITEPROMPT = 0x00000002,
            FOS_STRICTFILETYPES = 0x00000004,
            FOS_NOCHANGEDIR = 0x00000008,
            FOS_PICKFOLDERS = 0x00000020,
            FOS_FORCEFILESYSTEM = 0x00000040, // Ensure that items returned are filesystem items.
            FOS_ALLNONSTORAGEITEMS = 0x00000080, // Allow choosing items that have no storage.
            FOS_NOVALIDATE = 0x00000100,
            FOS_ALLOWMULTISELECT = 0x00000200,
            FOS_PATHMUSTEXIST = 0x00000800,
            FOS_FILEMUSTEXIST = 0x00001000,
            FOS_CREATEPROMPT = 0x00002000,
            FOS_SHAREAWARE = 0x00004000,
            FOS_NOREADONLYRETURN = 0x00008000,
            FOS_NOTESTFILECREATE = 0x00010000,
            FOS_HIDEMRUPLACES = 0x00020000,
            FOS_HIDEPINNEDPLACES = 0x00040000,
            FOS_NODEREFERENCELINKS = 0x00100000,
            FOS_DONTADDTORECENT = 0x02000000,
            FOS_FORCESHOWHIDDEN = 0x10000000,
            FOS_DEFAULTNOMINIMODE = 0x20000000
        }

        [Flags]
        internal enum SHCONTF : uint
        {
            SHCONTF_CHECKING_FOR_CHILDREN = 0x0010,
            SHCONTF_FOLDERS = 0x0020,
            SHCONTF_NONFOLDERS = 0x0040,
            SHCONTF_INCLUDEHIDDEN = 0x0080,
            SHCONTF_INIT_ON_FIRST_NEXT = 0x0100,
            SHCONTF_NETPRINTERSRCH = 0x0200,
            SHCONTF_SHAREABLE = 0x0400,
            SHCONTF_STORAGE = 0x0800,
            SHCONTF_NAVIGATION_ENUM = 0x1000,
            SHCONTF_FASTITEMS = 0x2000,
            SHCONTF_FLATLIST = 0x4000,
            SHCONTF_ENABLE_ASYNC = 0x8000
        }

        [Flags]
        internal enum SHGDNF : uint
        {
            SHGDN_NORMAL = 0x00000000,
            SHGDN_INFOLDER = 0x00000001,
            SHGDN_FOREDITING = 0x0001000,
            SHGDN_FORADDRESSBAR = 0x0004000,
            SHGDN_FORPARSING = 0x0008000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        internal struct COMDLG_FILTERSPEC
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pszName;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pszSpec;
        }

        internal enum FDAP
        {
            FDAP_BOTTOM = 0x00000000,
            FDAP_TOP = 0x00000001,
        }

        internal enum FDE_SHAREVIOLATION_RESPONSE
        {
            FDESVR_DEFAULT = 0x00000000,
            FDESVR_ACCEPT = 0x00000001,
            FDESVR_REFUSE = 0x00000002
        }

        internal enum FDE_OVERWRITE_RESPONSE
        {
            FDEOR_DEFAULT = 0x00000000,
            FDEOR_ACCEPT = 0x00000001,
            FDEOR_REFUSE = 0x00000002
        }

        internal enum SIGDN : uint
        {
            SIGDN_NORMALDISPLAY = 0x00000000,           // SHGDN_NORMAL
            SIGDN_PARENTRELATIVEPARSING = 0x80018001,   // SHGDN_INFOLDER | SHGDN_FORPARSING
            SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,  // SHGDN_FORPARSING
            SIGDN_PARENTRELATIVEEDITING = 0x80031001,   // SHGDN_INFOLDER | SHGDN_FOREDITING
            SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,  // SHGDN_FORPARSING | SHGDN_FORADDRESSBAR
            SIGDN_FILESYSPATH = 0x80058000,             // SHGDN_FORPARSING
            SIGDN_URL = 0x80068000,                     // SHGDN_FORPARSING
            SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,     // SHGDN_INFOLDER | SHGDN_FORPARSING | SHGDN_FORADDRESSBAR
            SIGDN_PARENTRELATIVE = 0x80080001           // SHGDN_INFOLDER
        }

        internal enum HRESULT : long
        {
            S_FALSE = 0x0001,
            S_OK = 0x0000,
            E_INVALIDARG = 0x80070057,
            E_OUTOFMEMORY = 0x8007000E
        }

        // Property System structs and consts
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct PROPERTYKEY
        {
            internal Guid fmtid;
            internal uint pid;
        }

        internal enum SIATTRIBFLAGS
        {
            SIATTRIBFLAGS_AND = 0x00000001, // if multiple items and the attirbutes together.
            SIATTRIBFLAGS_OR = 0x00000002, // if multiple items or the attributes together.
            SIATTRIBFLAGS_APPCOMPAT = 0x00000003, // Call GetAttributes directly on the ShellFolder for multiple attributes
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DOS_HEADER
        {
            public UInt16 e_magic;		// Magic number
            public UInt16 e_cblp;		// Bytes on last page of file
            public UInt16 e_cp;			// Pages in file
            public UInt16 e_crlc;		// Relocations
            public UInt16 e_cparhdr;	// Size of header in paragraphs
            public UInt16 e_minalloc;	// Minimum extra paragraphs needed
            public UInt16 e_maxalloc;	// Maximum extra paragraphs needed
            public UInt16 e_ss;			// Initial (relative) SS value
            public UInt16 e_sp;			// Initial SP value
            public UInt16 e_csum;		// Checksum
            public UInt16 e_ip;			// Initial IP value
            public UInt16 e_cs;			// Initial (relative) CS value
            public UInt16 e_lfarlc;		// File address of relocation table
            public UInt16 e_ovno;		// Overlay number
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public UInt16[] e_res1;		// Reserved words
            public UInt16 e_oemid;		// OEM identifier (for e_oeminfo)
            public UInt16 e_oeminfo;	// OEM information; e_oemid specific
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public UInt16[] e_res2;		// Reserved words
            public Int32 e_lfanew;		// File address of new exe header
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_NT_HEADERS
        {
            public UInt32 Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER OptionalHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DATA_DIRECTORY
        {
            public UInt32 VirtualAddress;
            public UInt32 Size;
        }
        internal const UInt16 IMAGE_DOS_SIGNATURE = 0x5A4D; // MZ
        internal const UInt16 IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10b;
        internal const UInt16 IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b;

        public const int IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16;

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER
        {
            public UInt16 Magic;
            public Byte MajorLinkerVersion;
            public Byte MinorLinkerVersion;
            public UInt32 SizeOfCode;
            public UInt32 SizeOfInitializedData;
            public UInt32 SizeOfUninitializedData;
            public UInt32 AddressOfEntryPoint;
            public UInt32 BaseOfCode;
            public UInt32 BaseOfData;
            public UInt32 ImageBase;
            public UInt32 SectionAlignment;
            public UInt32 FileAlignment;
            public UInt16 MajorOperatingSystemVersion;
            public UInt16 MinorOperatingSystemVersion;
            public UInt16 MajorImageVersion;
            public UInt16 MinorImageVersion;
            public UInt16 MajorSubsystemVersion;
            public UInt16 MinorSubsystemVersion;
            public UInt32 Win32VersionValue;
            public UInt32 SizeOfImage;
            public UInt32 SizeOfHeaders;
            public UInt32 CheckSum;
            public UInt16 Subsystem;
            public UInt16 DllCharacteristics;
            public UInt32 SizeOfStackReserve;
            public UInt32 SizeOfStackCommit;
            public UInt32 SizeOfHeapReserve;
            public UInt32 SizeOfHeapCommit;
            public UInt32 LoaderFlags;
            public UInt32 NumberOfRvaAndSizes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = IMAGE_NUMBEROF_DIRECTORY_ENTRIES)]
            public IMAGE_DATA_DIRECTORY[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_FILE_HEADER
        {
            public UInt16 Machine;
            public UInt16 NumberOfSections;
            public UInt32 TimeDateStamp;
            public UInt32 PointerToSymbolTable;
            public UInt32 NumberOfSymbols;
            public UInt16 SizeOfOptionalHeader;
            public UInt16 Characteristics;
        }

        internal const int IMAGE_SIZEOF_SHORT_NAME = 8;

        [StructLayout(LayoutKind.Explicit)]
        public struct IMAGE_SECTION_HEADER_MISC
        {
            [FieldOffset(0)]
            public UInt32 PhysicalAddress;
            [FieldOffset(0)]
            public UInt32 VirtualSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_SECTION_HEADER
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = IMAGE_SIZEOF_SHORT_NAME)]
            public Byte[] Name;
            public IMAGE_SECTION_HEADER_MISC Misc;
            public UInt32 VirtualAddress;
            public UInt32 SizeOfRawData;
            public UInt32 PointerToRawData;
            public UInt32 PointerToRelocations;
            public UInt32 PointerToLinenumbers;
            public UInt16 NumberOfRelocations;
            public UInt16 NumberOfLinenumbers;
            public UInt32 Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_EXPORT_DIRECTORY
        {
            public UInt32 Characteristics;
            public UInt32 TimeDateStamp;
            public UInt16 MajorVersion;
            public UInt16 MinorVersion;
            public UInt32 Name;
            public UInt32 Base;
            public UInt32 NumberOfFunctions;
            public UInt32 NumberOfNames;
            public UInt32 AddressOfFunctions;     // RVA from base of image
            public UInt32 AddressOfNames;         // RVA from base of image
            public UInt32 AddressOfNameOrdinals;  // RVA from base of image
        }

        internal const int IMAGE_DIRECTORY_ENTRY_EXPORT = 0;	// Export Directory

        internal const short IMAGE_FILE_EXECUTABLE_IMAGE = 0x0002; // File is executable  (i.e. no unresolved external references).
        internal const short IMAGE_FILE_DLL = 0x2000;	// File is a DLL.

        internal const short IMAGE_FILE_MACHINE_I386 = 0x014c;	// Intel 386.
        internal const short IMAGE_FILE_MACHINE_ARM = 0x01c0;		// ARM Little-Endian
        internal const short IMAGE_FILE_MACHINE_THUMB = 0x01c2;	// ARM Thumb/Thumb-2 Little-Endian
        internal const short IMAGE_FILE_MACHINE_ARMNT = 0x01c4;	// ARM Thumb-2 Little-Endian
        internal const short IMAGE_FILE_MACHINE_AMD64 = -31132;	// AMD64 (K8) (0x8664)

        internal const uint IMAGE_CURSOR = 0x0002; // Image to be loaded by LoadImage is a cursor
        internal const uint LR_DEFAULTCOLOR = 0x00000000; // Image to be loaded by LoadImage is to be default color
        internal const uint LR_LOADFROMFILE = 0x00000010; // Image to be loaded by LoadImage is to be from file
        internal const uint LR_DEFAULTSIZE = 0x00000040; // Image to be loaded by LoadImage is to be resized to default size for system DPI

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern int GetDoubleClickTime();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StrCmpLogicalW(String x, String y);

        // GUID{A520A1A4-1780-4FF6-BD18-167343C5AF16}
        internal static Guid FOLDERID_LocalAppDataLow = new Guid(new byte[] { 164, 161, 32, 165, 128, 23, 246, 79, 189, 24, 22, 115, 67, 197, 175, 22 });

        internal const UInt32 INVALID_FILE_ATTRIBUTES = unchecked((UInt32)(-1));
        internal const UInt32 FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        internal const UInt32 FILE_ATTRIBUTE_VIRTUAL = 0x00010000;
        internal const UInt32 FILE_ATTRIBUTE_HIDDEN = 0x00000002;

        // From winerror.h
        internal const int ERROR_INVALID_FUNCTION = 1;
        internal const int ERROR_FILE_NOT_FOUND = 2;
        internal const int ERROR_PATH_NOT_FOUND = 3;
        internal const int ERROR_ACCESS_DENIED = 5;
        internal const int ERROR_INVALID_DRIVE = 15;
        internal const int ERROR_NOT_READY = 21;
        internal const int ERROR_BAD_NETPATH = 53;
        internal const int ERROR_NETNAME_DELETED = 64;
        internal const int ERROR_NETWORK_ACCESS_DENIED = 65;
        internal const int ERROR_BAD_NET_NAME = 67;
        internal const int ERROR_INVALID_PARAMETER = 87;
        internal const int ERROR_INVALID_NAME = 123;
        internal const int ERROR_BAD_PATHNAME = 161;
        internal const int ERROR_NOT_FOUND = 1168;
        internal const int ERROR_DISK_CORRUPT = 1393;

        // From WinNt.h

        //#define FILE_ATTRIBUTE_READONLY             0x00000001
        //#define FILE_ATTRIBUTE_HIDDEN               0x00000002
        //#define FILE_ATTRIBUTE_SYSTEM               0x00000004
        //#define FILE_ATTRIBUTE_DIRECTORY            0x00000010
        //#define FILE_ATTRIBUTE_ARCHIVE              0x00000020
        //#define FILE_ATTRIBUTE_DEVICE               0x00000040
        //#define FILE_ATTRIBUTE_NORMAL               0x00000080
        //#define FILE_ATTRIBUTE_TEMPORARY            0x00000100
        //#define FILE_ATTRIBUTE_SPARSE_FILE          0x00000200
        //#define FILE_ATTRIBUTE_REPARSE_POINT        0x00000400
        //#define FILE_ATTRIBUTE_COMPRESSED           0x00000800
        //#define FILE_ATTRIBUTE_OFFLINE              0x00001000
        //#define FILE_ATTRIBUTE_NOT_CONTENT_INDEXED  0x00002000
        //#define FILE_ATTRIBUTE_ENCRYPTED            0x00004000
        //#define FILE_ATTRIBUTE_VIRTUAL              0x00010000

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetFileAttributes"), SuppressUnmanagedCodeSecurity]
        private static extern UInt32 GetFileAttributesPrivate(string lpFileName);

        private static uint GetFileAttributesInternal(string path)
        {
            UInt32 result = NativeMethods.GetFileAttributesPrivate(path);
            if (result == NativeMethods.INVALID_FILE_ATTRIBUTES)
            {
                int lastError = Marshal.GetLastWin32Error();
                switch (lastError)
                {
                    // These are our known "path invalid or does not exist" errors.
                    case ERROR_NOT_FOUND:
                    case ERROR_FILE_NOT_FOUND:
                    case ERROR_PATH_NOT_FOUND:
                    case ERROR_INVALID_DRIVE:
                    case ERROR_INVALID_NAME:
                    case ERROR_BAD_PATHNAME:
                    case ERROR_BAD_NET_NAME:
                    case ERROR_BAD_NETPATH:
                    case ERROR_NETNAME_DELETED:
                    case ERROR_INVALID_PARAMETER:
                    case ERROR_INVALID_FUNCTION:
                    case ERROR_NOT_READY:
                    case ERROR_DISK_CORRUPT:
                        return result;
                    case ERROR_ACCESS_DENIED:
                    case ERROR_NETWORK_ACCESS_DENIED:
                        throw new UnauthorizedAccessException(String.Format(CultureInfo.InvariantCulture, "{0} : '{1}'", ErrorHandler.LastErrorToString(lastError), path));
                    default:
                        // The fail is here to flush out unexpected rights issues
                        string errorText = ErrorHandler.LastErrorToString(lastError);
                        Debug.Fail(String.Format(CultureInfo.InvariantCulture, "Failed to get attributes for '{2}': {0} - {1}", lastError, errorText, path));
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the file attributes if possible.
        /// </summary>
        /// <exception cref="System.UnauthorizedAccessException">Thrown if the current user does not have rights to the path in question.</exception>
        /// <returns>'false' if the path is not valid</returns>
        internal static bool GetFileAttributes(string path, out FileAttributes attributes)
        {
            attributes = FileAttributes.Temporary;
            UInt32 result = NativeMethods.GetFileAttributesInternal(path);
            if (result == NativeMethods.INVALID_FILE_ATTRIBUTES)
            {
                return false;
            }

            result &= ~NativeMethods.FILE_ATTRIBUTE_VIRTUAL;
            attributes = (FileAttributes)result;

            return true;
        }


        internal static bool PathExists(string path)
        {
            return NativeMethods.GetFileAttributesInternal(path) != NativeMethods.INVALID_FILE_ATTRIBUTES;
        }

        internal static bool FileExists(string path)
        {
            uint attributes = NativeMethods.GetFileAttributesInternal(path);
            if (attributes == NativeMethods.INVALID_FILE_ATTRIBUTES)
            {
                // Nothing there or bad path name
                return false;
            }
            return (attributes & NativeMethods.FILE_ATTRIBUTE_DIRECTORY) != NativeMethods.FILE_ATTRIBUTE_DIRECTORY;
        }

        internal static bool DirectoryExists(string path)
        {
            uint attributes = NativeMethods.GetFileAttributesInternal(path);
            if (attributes == NativeMethods.INVALID_FILE_ATTRIBUTES)
            {
                // Nothing there or bad path name
                return false;
            }
            return (attributes & NativeMethods.FILE_ATTRIBUTE_DIRECTORY) == NativeMethods.FILE_ATTRIBUTE_DIRECTORY;
        }

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetFileAttributes(
             string lpFileName,
             [MarshalAs(UnmanagedType.U4)] FileAttributes dwFileAttributes);

        [DllImport("User32", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern NativeMethods.CursorHandle LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyCursor(IntPtr hCursor);

        [DllImport("User32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadCursorFromFile(string lpFileName);

        // BOOL WINAPI FreeLibrary(__in HMODULE hModule)
        /// <summary>
        /// Frees the given module handle. Do not use with handles obtained from GetModuleHandle (they aren't refcounted).
        /// </summary>
        /// <param name="hModule">The handle to free.</param>
        /// <returns>'true' if successful, use GetLastError if failed</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(SafeModuleHandle hModule);

        // This is a temporary workaround for P0 bugs devdiv2:202267 and devdiv2:203675
        // The work to implement the proper fix is being tracked by task devdiv2:217676
        // This code should be removed with devdiv2:217676 is closed.
        [DllImport("User32", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("User32", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetFocus();

        [Flags]
        public enum SHGFI : uint
        {
            /// <summary>get icon</summary>
            Icon = 0x000000100,
            /// <summary>get display name</summary>
            DisplayName = 0x000000200,
            /// <summary>get type name</summary>
            TypeName = 0x000000400,
            /// <summary>get attributes</summary>
            Attributes = 0x000000800,
            /// <summary>get icon location</summary>
            IconLocation = 0x000001000,
            /// <summary>return exe type</summary>
            ExeType = 0x000002000,
            /// <summary>get system icon index</summary>
            SysIconIndex = 0x000004000,
            /// <summary>put a link overlay on icon</summary>
            LinkOverlay = 0x000008000,
            /// <summary>show icon in selected state</summary>
            Selected = 0x000010000,
            /// <summary>get only specified attributes</summary>
            Attr_Specified = 0x000020000,
            /// <summary>get large icon</summary>
            LargeIcon = 0x000000000,
            /// <summary>get small icon</summary>
            SmallIcon = 0x000000001,
            /// <summary>get open icon</summary>
            OpenIcon = 0x000000002,
            /// <summary>get shell size icon</summary>
            ShellIconSize = 0x000000004,
            /// <summary>pszPath is a pidl</summary>
            PIDL = 0x000000008,
            /// <summary>use passed dwFileAttribute</summary>
            UseFileAttributes = 0x000000010,
            /// <summary>apply the appropriate overlays</summary>
            AddOverlays = 0x000000020,
            /// <summary>Get the index of the overlay in the upper 8 bits of the iIcon</summary>
            OverlayIndex = 0x000000040,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, SHGFI uFlags);

        internal static uint GET_MODULE_HANDLE_EX_FLAG_PIN = 0x00000001;
        internal static uint GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT = 0x00000002;
        internal static uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;

        [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetModuleHandleEx(
            [In]UInt32 dwFlags,
            [In, MarshalAs(UnmanagedType.LPStr)]string lpModuleName,
            [Out]out IntPtr hModule);


        /// <summary>
        /// Formats a file size in to a readable string
        /// </summary>
        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern long StrFormatByteSize(long fileSize, StringBuilder buffer, int bufferSize);

        internal sealed class CursorHandle : SafeHandle
        {
            private CursorHandle()
                : base(IntPtr.Zero, ownsHandle: true)
            {
            }

            public override bool IsInvalid
            {
                get
                {
                    return this.handle == IntPtr.Zero;
                }
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.DestroyCursor(this.handle);
            }
        }

        [DllImport("powrprof.dll", SetLastError = true)]
        internal static extern UInt32 CallNtPowerInformation(
              Int32 InformationLevel,
              IntPtr lpInputBuffer,
              UInt32 nInputBufferSize,
              IntPtr lpOutputBuffer,
              UInt32 nOutputBufferSize
              );

        internal const int LASTWAKETIME = 14;
    }
}
