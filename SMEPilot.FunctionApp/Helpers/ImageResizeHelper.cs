using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Helper for resizing and compressing images before embedding in documents
    /// </summary>
    public static class ImageResizeHelper
    {
        /// <summary>
        /// Resizes and compresses an image to reduce document size
        /// </summary>
        /// <param name="input">Original image bytes</param>
        /// <param name="maxWidth">Maximum width in pixels (default: 1200)</param>
        /// <param name="jpegQuality">JPEG quality 1-100 (default: 80)</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>Resized and compressed image bytes (JPEG format)</returns>
        public static byte[] ResizeAndCompressImage(byte[] input, int maxWidth = 1200, int jpegQuality = 80, ILogger? logger = null)
        {
            if (input == null || input.Length == 0)
                throw new ArgumentException("Input image bytes cannot be null or empty", nameof(input));

            if (maxWidth <= 0)
                throw new ArgumentException("Max width must be greater than 0", nameof(maxWidth));

            if (jpegQuality < 1 || jpegQuality > 100)
                jpegQuality = 80; // Default to 80 if invalid

            try
            {
                using var image = Image.Load(input);
                var originalSize = input.Length;
                var originalWidth = image.Width;
                var originalHeight = image.Height;

                // Only resize if image is larger than maxWidth
                if (image.Width > maxWidth)
                {
                    var ratio = (double)maxWidth / image.Width;
                    var newHeight = (int)(image.Height * ratio);
                    
                    image.Mutate(x => x.Resize(maxWidth, newHeight, KnownResamplers.Lanczos3));
                    logger?.LogDebug("🖼️ [IMAGE] Resized from {OriginalWidth}x{OriginalHeight} to {NewWidth}x{NewHeight}", 
                        originalWidth, originalHeight, maxWidth, newHeight);
                }

                // Convert to JPEG and compress
                using var ms = new MemoryStream();
                var encoder = new JpegEncoder { Quality = jpegQuality };
                image.SaveAsJpeg(ms, encoder);
                
                var compressedSize = ms.Length;
                var compressionRatio = originalSize > 0 ? (1.0 - (double)compressedSize / originalSize) * 100 : 0;
                
                logger?.LogDebug("🖼️ [IMAGE] Compressed from {OriginalSize} bytes to {CompressedSize} bytes ({CompressionRatio:F1}% reduction)", 
                    originalSize, compressedSize, compressionRatio);

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "⚠️ [IMAGE] Failed to resize/compress image, using original: {Error}", ex.Message);
                // Return original if resize fails
                return input;
            }
        }

        /// <summary>
        /// Determines if an image should be resized based on size thresholds
        /// </summary>
        /// <param name="imageBytes">Image bytes</param>
        /// <param name="sizeThreshold">Size threshold in bytes (default: 500KB)</param>
        /// <returns>True if image should be resized</returns>
        public static bool ShouldResize(byte[] imageBytes, long sizeThreshold = 500 * 1024)
        {
            return imageBytes != null && imageBytes.Length > sizeThreshold;
        }
    }
}

