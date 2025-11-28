using System;
using System.IO;

namespace SMEPilot.FunctionApp.TestFiles
{
    class Program
    {
        static void Main(string[] args)
        {
            var testFilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestFiles");
            if (!Directory.Exists(testFilesDir))
            {
                Directory.CreateDirectory(testFilesDir);
            }

            var rawDocxPath = Path.Combine(testFilesDir, "example_proper_raw.docx");
            var templateDotxPath = Path.Combine(testFilesDir, "example_proper_template.dotx");

            Console.WriteLine("Creating example_proper_raw.docx...");
            CreateExampleFiles.CreateExampleRawDocx(rawDocxPath);
            Console.WriteLine($"✓ Created: {rawDocxPath}");

            Console.WriteLine("Creating example_proper_template.dotx...");
            CreateExampleFiles.CreateExampleTemplateDotx(templateDotxPath);
            Console.WriteLine($"✓ Created: {templateDotxPath}");

            Console.WriteLine("\nDone! Files created successfully.");
        }
    }
}


