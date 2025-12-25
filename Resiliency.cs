using System;
using System.Security.Cryptography;
using System.Text;

namespace FileSystemContainer
{
    public static class Resiliency
    {
        // Изчисляване на контролна сума за данни
        public static uint CalculateChecksum(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            uint checksum = 0x12345678; // Начална стойност

            for (int i = 0; i < data.Length; i++)
            {
                checksum = (checksum << 4) ^ (checksum >> 28) ^ data[i];
            }

            return checksum;
        }

        // Изчисляване на CRC32
        public static uint CalculateCRC32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            uint[] table = GenerateCRC32Table();

            for (int i = 0; i < data.Length; i++)
            {
                byte index = (byte)(((crc) & 0xFF) ^ data[i]);
                crc = (crc >> 8) ^ table[index];
            }

            return ~crc;
        }

        private static uint[] GenerateCRC32Table()
        {
            uint[] table = new uint[256];
            uint polynomial = 0xEDB88320;

            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }

            return table;
        }

        // Генериране на хеш за проверка на целостта
        public static byte[] GenerateIntegrityHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }

        // Проверка на целостта
        public static bool VerifyIntegrity(byte[] data, uint storedChecksum)
        {
            uint calculatedChecksum = CalculateChecksum(data);
            return calculatedChecksum == storedChecksum;
        }

        // Проверка на хеш
        public static bool VerifyHash(byte[] data, byte[] storedHash)
        {
            byte[] calculatedHash = GenerateIntegrityHash(data);

            if (calculatedHash.Length != storedHash.Length)
                return false;

            for (int i = 0; i < calculatedHash.Length; i++)
            {
                if (calculatedHash[i] != storedHash[i])
                    return false;
            }

            return true;
        }
    }
}