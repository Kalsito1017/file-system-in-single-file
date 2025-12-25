using System;
using System.IO;
using System.IO.Compression;

namespace FileSystemContainer
{
    public static class Compression
    {
        // Компресиране на данни
        public static byte[] CompressData(byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    gzipStream.Write(data, 0, data.Length);
                }
                return memoryStream.ToArray();
            }
        }

        // Декомпресиране на данни
        public static byte[] DecompressData(byte[] compressedData, uint originalSize)
        {
            using (var compressedStream = new MemoryStream(compressedData))
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                gzipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        // Прост алгоритъм за компресия (RLE) за малки файлове
        public static byte[] SimpleCompress(byte[] data)
        {
            if (data.Length == 0) return data;

            using (var ms = new MemoryStream())
            {
                byte current = data[0];
                int count = 1;

                for (int i = 1; i < data.Length; i++)
                {
                    if (data[i] == current && count < 255)
                    {
                        count++;
                    }
                    else
                    {
                        ms.WriteByte((byte)count);
                        ms.WriteByte(current);
                        current = data[i];
                        count = 1;
                    }
                }

                ms.WriteByte((byte)count);
                ms.WriteByte(current);

                return ms.ToArray();
            }
        }

        public static byte[] SimpleDecompress(byte[] compressedData)
        {
            using (var ms = new MemoryStream())
            using (var compressedStream = new MemoryStream(compressedData))
            {
                while (compressedStream.Position < compressedStream.Length)
                {
                    int count = compressedStream.ReadByte();
                    if (count == -1) break;

                    int value = compressedStream.ReadByte();
                    if (value == -1) break;

                    for (int j = 0; j < count; j++)
                    {
                        ms.WriteByte((byte)value);
                    }
                }

                return ms.ToArray();
            }
        }
    }
}