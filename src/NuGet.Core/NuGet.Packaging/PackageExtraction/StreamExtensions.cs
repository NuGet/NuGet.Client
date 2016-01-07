using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public static class StreamExtensions
    {
        public static async Task<string> CopyToFileAsync(this Stream inputStream, string fileFullPath, CancellationToken token)
        {
            if (Path.GetFileName(fileFullPath).Length == 0)
            {
                Directory.CreateDirectory(fileFullPath);
                return fileFullPath;
            }

            var directory = Path.GetDirectoryName(fileFullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(fileFullPath))
            {
                // Log and skip adding file
                return fileFullPath;
            }

            const int DefaultBufferSize = 4096;
            using (var outputStream = File.Create(fileFullPath, DefaultBufferSize, FileOptions.Asynchronous))
            {
                await inputStream.CopyToAsync(outputStream, DefaultBufferSize, token);
            }

            return fileFullPath;
        }
    }
}
