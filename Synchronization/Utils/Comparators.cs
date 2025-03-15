using System.Security.Cryptography;

namespace Synchronization.Utils;
public static class Comparators
{
    public static async Task<bool> CompareMD5Async(string filePath1, string filePath2, CancellationToken cancellationToken)
    {
        using MD5 md5 = MD5.Create();
        using FileStream stream1 = new(filePath1, FileMode.Open, FileAccess.Read);
        using FileStream stream2 = new(filePath2, FileMode.Open, FileAccess.Read);

        byte[] hash1 = await md5.ComputeHashAsync(stream1);
        byte[] hash2 = await md5.ComputeHashAsync(stream2);
        return hash1.SequenceEqual(hash2);
    }

    public static async Task<bool> CompareSHA256Async(string filePath1, string filePath2, CancellationToken cancellationToken)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream1 = new(filePath1, FileMode.Open, FileAccess.Read);
        using FileStream stream2 = new(filePath2, FileMode.Open, FileAccess.Read);

        byte[] hash1 = await sha256.ComputeHashAsync(stream1);
        byte[] hash2 = await sha256.ComputeHashAsync(stream2);
        return hash1.SequenceEqual(hash2);
    }
    public static async Task<bool> CompareFilesBinaryAsync(string filePath1, string filePath2, CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 1024; // 1 mb buffer size for efficient reading

        using (FileStream stream1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous))
        using (FileStream stream2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous))
        {
            if (stream1.Length != stream2.Length)
            {
                return false; // Files are different sizes
            }

            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = new byte[bufferSize];

            while (true)
            {
                int bytesRead1 = await stream1.ReadAsync(buffer1, 0, bufferSize);
                int bytesRead2 = await stream2.ReadAsync(buffer2, 0, bufferSize);

                if (bytesRead1 != bytesRead2)
                {
                    return false; // Files have different content lengths
                }

                if (bytesRead1 == 0)
                {
                    return true; // End of both files reached, and all bytes matched
                }

                if (!buffer1.SequenceEqual(buffer2))
                {
                    return false; // Bytes do not match
                }
            }
        }

    }
}
