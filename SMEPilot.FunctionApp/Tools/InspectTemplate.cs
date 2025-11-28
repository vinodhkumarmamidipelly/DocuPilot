using System;
using System.IO;
using SMEPilot.FunctionApp.Helpers;

namespace SMEPilot.FunctionApp.Tools
{
    /// <summary>
    /// Standalone tool to inspect Word templates
    /// Usage: Run this as a console app or call from Program.cs
    /// </summary>
    public class InspectTemplateTool
    {
        public static void Run(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
            {
                Console.WriteLine("❌ Please provide a template path");
                Console.WriteLine("Usage: InspectTemplate <path-to-template.dotx>");
                return;
            }

            if (!File.Exists(templatePath))
            {
                Console.WriteLine($"❌ Template file not found: {templatePath}");
                return;
            }

            var inspector = new TemplateInspector();
            var result = inspector.InspectTemplate(templatePath);
            inspector.PrintReport(result);
        }
    }
}

