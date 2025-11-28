using System;
using System.IO;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Helper to create a unique temp folder per job and clean up in Dispose.
    /// Usage: using(var lease = new TempFileLease("smepilot")) { var p = lease.GetPath("name.docx"); ... }
    /// </summary>
    public sealed class TempFileLease : IDisposable
    {
        public string TempFolder { get; }

        /// <summary>
        /// Creates a unique temporary folder for file operations
        /// </summary>
        /// <param name="prefix">Prefix for the temp folder name (default: "smepilot")</param>
        public TempFileLease(string prefix = "smepilot")
        {
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "smepilot";

            var baseTemp = Path.GetTempPath();
            TempFolder = Path.Combine(baseTemp, $"{prefix}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(TempFolder);
        }

        /// <summary>
        /// Gets a file path within the temp folder
        /// </summary>
        /// <param name="fileName">Name of the file</param>
        /// <returns>Full path to the file in the temp folder</returns>
        public string GetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

            // Sanitize filename to avoid path traversal
            var safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
                throw new ArgumentException("Invalid file name", nameof(fileName));

            return Path.Combine(TempFolder, safeFileName);
        }

        /// <summary>
        /// Cleans up the temporary folder and all its contents
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(TempFolder))
                {
                    Directory.Delete(TempFolder, true);
                }
            }
            catch
            {
                // Best-effort cleanup. Don't throw from Dispose.
                // Logging could be added here if needed, but we avoid dependencies in Dispose.
            }
        }
    }
}

