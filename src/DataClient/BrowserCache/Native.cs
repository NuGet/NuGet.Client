using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NuGet.Data
{
    /// <summary>
    /// Native calls for the browser cache
    /// </summary>
    internal class Native
    {
        [DllImport("Wininet.dll", PreserveSig = true, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr RetrieveUrlCacheEntryStream(string unescapedUrl, IntPtr info, ref uint lpcbCacheEntryInfo, [MarshalAs(UnmanagedType.Bool)] bool fRandomRead, uint dwReserved);

        [DllImport("Wininet.dll", PreserveSig = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadUrlCacheEntryStream(IntPtr hUrlCacheStream, uint dwLocation, [In, Out] IntPtr lpBuffer, ref uint lpdwLen, uint reserved);

        [DllImport("Wininet.dll", PreserveSig = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnlockUrlCacheEntryStream(IntPtr hUrlCacheStream, uint dwReserved);

        [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CommitUrlCacheEntry([In] string urlName, [In] string localFileName, [In] System.Runtime.InteropServices.ComTypes.FILETIME expireTime, [In] System.Runtime.InteropServices.ComTypes.FILETIME lastModifiedTime, [In] EntryType EntryType, [In] string headerInfo, [In] int headerSizeTChars, [In] string fileExtension, [In] string originalUrl);

        [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateUrlCacheEntry([In] string urlName, [In] int expectedFileSize, [In] string fileExtension, [Out] StringBuilder fileName, [In] int dwReserved);

        [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool DeleteUrlCacheEntry([In] string urlName);

        [StructLayout(LayoutKind.Sequential)]
        public struct INTERNET_CACHE_ENTRY_INFO
        {
            public UInt32 dwStructSize;
            public string lpszSourceUrlName;
            public string lpszLocalFileName;
            public UInt32 CacheEntryType;
            public UInt32 dwUseCount;
            public UInt32 dwHitRate;
            public UInt32 dwSizeLow;
            public UInt32 dwSizeHigh;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastModifiedTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ExpireTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastSyncTime;
            public IntPtr lpHeaderInfo;
            public UInt32 dwHeaderInfoSize;
            public string lpszFileExtension;
            public UInt32 dwExemptDelta;
        };

        [Flags]
        public enum EntryType
        {
            Edited = 8,
            TrackOffline = 0x10,
            TrackOnline = 0x20,
            NormalEntry = 0x41,
            StickyEntry = 0x44,
            Sparse = 0x10000,
            Cookie = 0x100000,
            UrlHistory = 0x200000,
        }
    }
}
