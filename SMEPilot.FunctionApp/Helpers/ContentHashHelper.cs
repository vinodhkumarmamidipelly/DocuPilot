using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Helper for computing content hashes for idempotency checks
    /// </summary>
    public static class ContentHashHelper
    {
        /// <summary>
        /// Computes SHA256 hash of a file and returns as hexadecimal string
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>SHA256 hash as lowercase hexadecimal string</returns>
        public static string ComputeSha256Hex(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Computes SHA256 hash of a stream and returns as hexadecimal string
        /// </summary>
        /// <param name="stream">Stream to hash</param>
        /// <returns>SHA256 hash as lowercase hexadecimal string</returns>
        public static string ComputeSha256Hex(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var originalPosition = stream.Position;
            try
            {
                stream.Position = 0;
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        /// <summary>
        /// Computes SHA256 hash of byte array and returns as hexadecimal string
        /// </summary>
        /// <param name="bytes">Byte array to hash</param>
        /// <returns>SHA256 hash as lowercase hexadecimal string</returns>
        public static string ComputeSha256Hex(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}

