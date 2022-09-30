// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    public static class ConcurrencyUtilities
    {
        private const int NumberOfRetries = 3000;
        private static readonly TimeSpan SleepDuration = TimeSpan.FromMilliseconds(10);

        public async static Task<T> ExecuteWithFileLockedAsync<T>(string filePath,
            Func<CancellationToken, Task<T>> action,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // limit the number of unauthorized, this should be around 30 seconds.
            var unauthorizedAttemptsLeft = NumberOfRetries;

            while (true)
            {
                FileStream fs = null;
                var lockPath = string.Empty;

                try
                {
                    try
                    {
                        lockPath = FileLockPath(filePath);

                        fs = AcquireFileStream(lockPath);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        throw;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        token.ThrowIfCancellationRequested();

                        if (unauthorizedAttemptsLeft < 1)
                        {
                            if (string.IsNullOrEmpty(lockPath))
                            {
                                lockPath = BasePath;
                            }

                            var message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.UnauthorizedLockFail,
                                lockPath,
                                filePath);

                            throw new InvalidOperationException(message);
                        }

                        unauthorizedAttemptsLeft--;

                        // This can occur when the file is being deleted
                        // Or when an admin user has locked the file
                        await Task.Delay(SleepDuration);
                        continue;
                    }
                    catch (IOException)
                    {
                        token.ThrowIfCancellationRequested();

                        await Task.Delay(SleepDuration);
                        continue;
                    }

                    // Run the action within the lock
                    return await action(token);
                }
                finally
                {
                    if (fs != null)
                    {
                        // Dispose of the stream, this will cause a delete
                        fs.Dispose();
                    }
                }
            }
        }

        public static void ExecuteWithFileLocked(string filePath,
            Action action)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // limit the number of unauthorized, this should be around 30 seconds.
            var unauthorizedAttemptsLeft = NumberOfRetries;

            while (true)
            {
                FileStream fs = null;
                var lockPath = string.Empty;
                try
                {
                    try
                    {
                        lockPath = FileLockPath(filePath);

                        fs = AcquireFileStream(lockPath);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        throw;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        if (unauthorizedAttemptsLeft < 1)
                        {
                            if (string.IsNullOrEmpty(lockPath))
                            {
                                lockPath = BasePath;
                            }

                            var message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.UnauthorizedLockFail,
                                lockPath,
                                filePath);

                            throw new InvalidOperationException(message);
                        }

                        unauthorizedAttemptsLeft--;

                        // This can occur when the file is being deleted
                        // Or when an admin user has locked the file
                        Thread.Sleep(SleepDuration);
                        continue;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(SleepDuration);
                        continue;
                    }

                    // Run the action within the lock
                    action();
                    return;
                }
                finally
                {
                    // Dispose of the stream, this will cause a delete
                    fs?.Dispose();
                }
            }
        }

        private static FileStream AcquireFileStream(string lockPath)
        {
            FileOptions options;
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                // This file is deleted when the stream is closed.
                options = FileOptions.DeleteOnClose;
            }
            else
            {
                // FileOptions.DeleteOnClose causes concurrency issues on Mac OS X and Linux.
                options = FileOptions.None;
            }

            // Sync operations have shown much better performance than FileOptions.Asynchronous
            return new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 32,
                options: options);
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
                // To avoid conflicts on package id casing a case-insensitive lock is used.
                var fullPath = Path.IsPathRooted(filePath) ? Path.GetFullPath(filePath) : filePath;
                var normalizedPath = fullPath.ToUpperInvariant();

                var hash = sha.ComputeHash(Encoding.UTF32.GetBytes(normalizedPath));

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
    }
}
