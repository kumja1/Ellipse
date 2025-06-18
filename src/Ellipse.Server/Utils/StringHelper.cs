using System.IO.Compression;
using System.Text;

namespace Ellipse.Server.Utils.Helpers;

internal static class StringHelper
{
    /// <summary>
    /// Compresses the string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns></returns>
    public static string CompressString(string text)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            MemoryStream memoryStream = new();
            using GZipStream gZipStream = new(
                memoryStream,
                CompressionMode.Compress,
                leaveOpen: true
            );

            gZipStream.Write(buffer, 0, buffer.Length);
            gZipStream.Flush();
            memoryStream.Position = 0;

            byte[] compressedData = new byte[memoryStream.Length];
            memoryStream.ReadExactly(compressedData, 0, compressedData.Length);

            byte[] gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            return Convert.ToBase64String(gZipBuffer);
        }
        catch (Exception _)
        {
            Console.WriteLine($"[CompressString] Failed to compress string: {text}");
            throw;
        }
    }

    /// <summary>
    /// Decompresses the string.
    /// </summary>
    /// <param name="compressedText">The compressed text.</param>
    /// <returns></returns>
    public static string DecompressString(string compressedText)
    {
        try
        {
            byte[] gZipBuffer = Convert.FromBase64String(compressedText);
            using MemoryStream memoryStream = new MemoryStream();

            int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
            memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

            memoryStream.Position = 0;
            using GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using StreamReader reader = new StreamReader(gZipStream);
            return reader.ReadToEnd();
        }
        catch (Exception _)
        {
            Console.WriteLine($"[DecompressString] Failed to decompress string: {compressedText}");
            throw;
        }
    }
}
