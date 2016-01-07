// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    internal static class ConcurrencyUtilities
    {
        public async static Task<T> ExecuteWithFileLocked<T>(string filePath,
            Func<CancellationToken, Task<T>> action,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var lockPath = FileLockPath(filePath);
            var bytes = Encoding.UTF8.GetBytes($"{ProcessId}{Environment.NewLine}{filePath}{Environment.NewLine}");

            while (true)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (DirectoryNotFoundException)
                {
                    throw;
                }
                catch (IOException)
                {
                    token.ThrowIfCancellationRequested();

                    await Task.Delay(10);
                    continue;
                }

                try
                {
                    await fs.WriteAsync(bytes, 0, bytes.Length, token);
                }
                catch
                {
                }

                using (fs)
                {
                    return await action(token);
                }
            }
        }

        private static string _basePath;
        private static string BasePath
        {
            get
            {
                if (_basePath != null)
                {
                    return _basePath;
                }

                _basePath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp), "lock");

                //if (RuntimeEnvironmentHelper.IsWindows || !Directory.Exists("/var/lock/"))
                //{
                //    _basePath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp), "lock");
                //}
                //else
                //{
                //    _basePath = "/var/lock/nuget/";
                //}

                Directory.CreateDirectory(_basePath);

                return _basePath;
            }
        }

        private static string FileLockPath(string filePath)
        {
            // In case the directory was cleaned up, we can choose to fix it (at a cost of another roundtrip to disk
            // or fail, starting with the more expensive path, and we might have to get rid of it if it becomes too hot.
            Directory.CreateDirectory(BasePath);

            return Path.Combine(BasePath, FilePathToLockName(filePath));
        }

        private static string FilePathToLockName(string filePath)
        {
            // If we use a file path directly as the name of a semaphore,
            // the ctor of semaphore looks for the file and throws an IOException
            // when the file doesn't exist. So we need a conversion from a file path
            // to a unique lock name.
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF32.GetBytes(filePath));

                return ToHex(hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];

            for (int index = 0, outIndex = 0; index < bytes.Length; index++)
            {
                c[outIndex++] = ToHexChar(bytes[index] >> 4);
                c[outIndex++] = ToHexChar(bytes[index] & 0x0f);
            }

            return new string(c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char ToHexChar(int input)
        {
            if (input > 9)
            {
                return (char)(input + 0x57);
            }
            else
            {
                return (char)(input + 0x30);
            }
        }

        private static int _processId = -1;
        private static int ProcessId
        {
            get
            {
                if (_processId < 0)
                {
                    _processId = Process.GetCurrentProcess().Id;
                }

                return _processId;
            }
        }
    }
}
