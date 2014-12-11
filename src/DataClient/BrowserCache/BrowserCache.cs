using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;
using System.Text;
using System.IO;

namespace NuGet.Data
{
    /// <summary>
    /// Simple mshtml browser cache wrapper.
    /// </summary>
    internal static class BrowserCache
    {
        private const int MAX_PATH = 260;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_FILE_NOT_FOUND = 2;

        public static Stream Get(string url)
        {
            IntPtr bufferPtr = IntPtr.Zero;
            uint bufferSize = 0;
            bool randomAccess = false;

            Stream stream = null;

            try
            {
                Native.RetrieveUrlCacheEntryStream(url, bufferPtr, ref bufferSize, randomAccess, 0);

                int error = Marshal.GetLastWin32Error();

                if (error != ERROR_FILE_NOT_FOUND && error == ERROR_INSUFFICIENT_BUFFER)
                {
                    int newLength = unchecked((int)bufferSize);

                    bufferPtr = Marshal.AllocHGlobal(newLength);
                    Marshal.WriteInt32(bufferPtr, newLength);

                    IntPtr handle = Native.RetrieveUrlCacheEntryStream(url, bufferPtr, ref bufferSize, randomAccess, 0);

                    if (handle != IntPtr.Zero)
                    {
                        Native.INTERNET_CACHE_ENTRY_INFO info = (Native.INTERNET_CACHE_ENTRY_INFO)Marshal.PtrToStructure(bufferPtr, typeof(Native.INTERNET_CACHE_ENTRY_INFO));

                        try
                        {
                            byte[] entryData = GetInternal(handle, 0, info.dwSizeLow);

                            if (entryData != null)
                            {
                                stream = new MemoryStream(entryData);
                            }
                        }
                        finally
                        {
                            Native.UnlockUrlCacheEntryStream(handle, 0);
                        }
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                Debug.Fail("OutOfMemoryException");
                stream = null;
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
            }

            return stream;
        }

        private static byte[] GetInternal(IntPtr stream, uint offset, uint length)
        {
            byte[] bytes = new byte[length];

            try
            {
                IntPtr buffer = Marshal.AllocHGlobal(unchecked((int)length));

                try
                {
                    while (!Native.ReadUrlCacheEntryStream(stream, offset, buffer, ref length, 0))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ERROR_INSUFFICIENT_BUFFER)
                        {
                            Debug.Fail("Unable to retrieve from cache.");
                            return null;
                        }

                        Marshal.ReAllocHGlobal(buffer, (IntPtr)unchecked((int)length));
                        bytes = new byte[length];
                    }

                    // copy bytes
                    for (uint i = 0; i < length; i++)
                    {
                        bytes[i] = Marshal.ReadByte(buffer, unchecked((int)i));
                    }
                }
                finally
                {
                    if (buffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                Debug.Fail("OutOfMemoryException");
                bytes = null;
            }

            return bytes;
        }

        public static bool Add(string url, Stream stream, DateTime expires)
        {
            if (String.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException("url");
            }

            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (expires == null)
            {
                throw new ArgumentNullException("expires");
            }

            bool result = false;

            StringBuilder sb = new StringBuilder(MAX_PATH);
            if (Native.CreateUrlCacheEntry(url, (int)stream.Length, string.Empty, sb, 0))
            {
                string file = sb.ToString();

                // copy the stream to the url cache entry
                using (FileStream cacheStream = new FileStream(file, FileMode.Open))
                {
                    stream.CopyTo(cacheStream);
                    cacheStream.Close();
                }

                // commit the entry
                if (Native.CommitUrlCacheEntry(url, file,
                        GetFILETIME(GetUtcTime(expires)),
                        GetFILETIME(GetUtcTime(DateTime.Now)),
                        Native.EntryType.NormalEntry, null, 0,
                        null, url))
                {
                    result = true;
                }
                else
                {
                    // remove the failed entry
                    Native.DeleteUrlCacheEntry(url);
                }
            }

            Debug.Assert(result, "Unable to add cache entry!");

            return result;
        }

        private static long GetUtcTime(DateTime dateTime)
        {
            long result = 0;

            try
            {
                result = dateTime.ToFileTimeUtc();
            }
            catch (ArgumentOutOfRangeException)
            {
                // ignore exceptions from this
            }

            return result;
        }

        private static System.Runtime.InteropServices.ComTypes.FILETIME GetFILETIME(long time)
        {
            System.Runtime.InteropServices.ComTypes.FILETIME fileTime = new System.Runtime.InteropServices.ComTypes.FILETIME();
            fileTime.dwLowDateTime = (int)time;
            fileTime.dwHighDateTime = (int)(time >> 0x20);
            return fileTime;
        }
    }
}