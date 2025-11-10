using System;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Generates the organizational .dotx template file programmatically
    /// </summary>
    public static class TemplateGenerator
    {
        /// <summary>
        /// Creates the SMEPilot organizational template (.dotx file)
        /// </summary>
        /// <param name="outputPath">Path where template will be saved</param>
        public static void GenerateTemplate(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Template))
            {
                var mainPart = doc.AddMainDocumentPart();
                
                // Create styles part
                var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
                stylesPart.Styles = GenerateStyles();
                
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body;

                // Cover Page
                AddCoverPagePlaceholder(body);

                // TOC
                AddTableOfContentsPlaceholder(body);

                // Section placeholders
                AddSectionPlaceholders(body);

                // Revision History placeholder
                AddRevisionHistoryPlaceholder(body);

                mainPart.Document.Save();
            }
        }

        private static void AddCoverPagePlaceholder(Body body)
        {
            body.AppendChild(new Paragraph(new Run(new Text("SMEPilot Document")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Title" },
                    new SpacingBetweenLines() { After = "240" })
            });

            body.AppendChild(new Paragraph(new Run(new Text("Document Title: [ENTER TITLE HERE]")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "120" })
            });

            body.AppendChild(new Paragraph(new Run(new Text("Project / Module: [ENTER PROJECT NAME]")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "120" })
            });

            body.AppendChild(new Paragraph(new Run(new Text("Author: [ENTER AUTHOR NAME]")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "120" })
            });

            body.AppendChild(new Paragraph(new Run(new Text($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "120" })
            });

            body.AppendChild(new Paragraph(new Run(new Text("Classification: [To be filled by enrichment logic]")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "120" })
            });

            body.AppendChild(new Paragraph(new Run(new Text("Version: 1.0")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "480" })
            });

            body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
        }

        private static void AddTableOfContentsPlaceholder(Body body)
        {
            body.AppendChild(new Paragraph(new Run(new Text("Table of Contents")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "TOCHeading" },
                    new SpacingBetweenLines() { After = "240" })
            });

            // TOC Field
            var tocParagraph = new Paragraph();
            var tocRun = new Run();
            
            tocRun.AppendChild(new FieldChar() { FieldCharType = FieldCharValues.Begin });
            tocRun.AppendChild(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });
            tocRun.AppendChild(new FieldChar() { FieldCharType = FieldCharValues.Separate });
            tocRun.AppendChild(new FieldChar() { FieldCharType = FieldCharValues.End });
            
            tocParagraph.AppendChild(tocRun);
            tocParagraph.ParagraphProperties = new ParagraphProperties(
                new SpacingBetweenLines() { After = "240" });
            
            body.AppendChild(tocParagraph);
            body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
        }

        private static void AddSectionPlaceholders(Body body)
        {
            var sections = new[]
            {
                "1. Introduction",
                "2. Overview",
                "3. Functional Details",
                "4. Technical Details",
                "5. Screenshots",
                "6. Troubleshooting & FAQ"
            };

            foreach (var section in sections)
            {
                body.AppendChild(new Paragraph(new Run(new Text(section)))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId() { Val = "Heading1" },
                        new SpacingBetweenLines() { After = "240" })
                });

                body.AppendChild(new Paragraph(new Run(new Text($"[Add {section.ToLower()} content here or auto-filled by enricher]")))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId() { Val = "Normal" },
                        new SpacingBetweenLines() { After = "240" })
                });

                body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
            }
        }

        private static void AddRevisionHistoryPlaceholder(Body body)
        {
            body.AppendChild(new Paragraph(new Run(new Text("7. Revision History")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Heading1" },
                    new SpacingBetweenLines() { Before = "480", After = "240" })
            });

            // Create placeholder table
            var table = new Table(
                new TableProperties(
                    new TableBorders(
                        new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 })),
                
                new TableRow(
                    new TableCell(new Paragraph(new Run(new Text("Date")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableHeading" })
                    }),
                    new TableCell(new Paragraph(new Run(new Text("Author")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableHeading" })
                    }),
                    new TableCell(new Paragraph(new Run(new Text("Change Summary")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableHeading" })
                    }))
            );

            body.AppendChild(table);
        }

        private static Styles GenerateStyles()
        {
            var styles = new Styles();
            
            // Title style
            styles.Append(new Style(new StyleName() { Val = "Title" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Title",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "240" })
            });

            // Subtitle style
            styles.Append(new Style(new StyleName() { Val = "Subtitle" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Subtitle"
            });

            // Heading1 style
            styles.Append(new Style(new StyleName() { Val = "Heading 1" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading1",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines() { Before = "240", After = "120" })
            });

            // TOCHeading style
            styles.Append(new Style(new StyleName() { Val = "TOC Heading" }, new BasedOn() { Val = "Heading1" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "TOCHeading"
            });

            // Caption style
            styles.Append(new Style(new StyleName() { Val = "Caption" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Caption",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new SpacingBetweenLines() { After = "120" })
            });

            // Table styles
            styles.Append(new Style(new StyleName() { Val = "Table Heading" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "TableHeading"
            });

            styles.Append(new Style(new StyleName() { Val = "Table Content" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "TableContent"
            });

            return styles;
        }
    }
}

