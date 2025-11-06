using DocumentFormat.OpenXml.Packaging;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SMEPilot.FunctionApp.Helpers
{
    public class SimpleExtractor
    {
        public async Task<(string Text, List<byte[]> Images)> ExtractDocxAsync(Stream docxStream)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            using var ms = new MemoryStream();
            await docxStream.CopyToAsync(ms);
            ms.Position = 0;

            WordprocessingDocument? doc = null;
            try
            {
                doc = WordprocessingDocument.Open(ms, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    textBuilder.AppendLine(body.InnerText);
                }

                // Extract images
                var imageParts = doc.MainDocumentPart?.ImageParts;
                if (imageParts != null)
                {
                    foreach (var imgPart in imageParts)
                    {
                        using var imgStream = imgPart.GetStream();
                        using var ims = new MemoryStream();
                        await imgStream.CopyToAsync(ims);
                        images.Add(ims.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                // If stream is not a valid DOCX (e.g., mock content), extract raw text
                Console.WriteLine($"Warning: Stream is not a valid DOCX file: {ex.Message}. Attempting to extract as plain text.");
                ms.Position = 0;
                using var reader = new StreamReader(ms);
                textBuilder.AppendLine(await reader.ReadToEndAsync());
                // No images for mock content
            }
            finally
            {
                doc?.Dispose();
            }

            return (textBuilder.ToString(), images);
        }
    }
}
