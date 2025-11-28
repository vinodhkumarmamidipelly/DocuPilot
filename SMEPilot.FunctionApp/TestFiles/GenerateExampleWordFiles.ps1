# PowerShell script to generate example Word documents
# This script uses .NET to create .docx and .dotx files

$ErrorActionPreference = "Stop"

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testFilesDir = $scriptDir

# Create C# code to generate the files
$csharpCode = @"
using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

public class WordGenerator
{
    public static void CreateRawDocx(string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var body = mainPart.Document.Body;

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = CreateStyles();

        AddHeading(body, "FUNCTIONAL SPECIFICATION DOCUMENT (FSD)", 1);
        AddParagraph(body, "Project Name: Sample Fitness App");
        AddParagraph(body, "Version: 1.0");
        AddParagraph(body, "Date: December 2024");
        AddParagraph(body, "Status: Draft for Review");
        AddParagraph(body, "Project: Sample Fitness App - Trainer & Client Management Platform");
        AddEmptyLine(body);

        AddHeading(body, "1. PROJECT OVERVIEW", 2);
        AddHeading(body, "1.1 Project Information", 3);
        AddParagraph(body, "Project Name: Sample Fitness App");
        AddParagraph(body, "Project Description: A mobile-first Progressive Web App (PWA) that enables gym trainers to manage clients, share workout videos, and track progress.");
        AddParagraph(body, "Project Objectives:");
        AddBullet(body, "Enable trainers to efficiently manage multiple clients");
        AddBullet(body, "Provide clients with easy-to-use workout logging tools");
        AddBullet(body, "Track and visualize fitness progress over time");
        AddParagraph(body, "Project Scope:");
        AddBullet(body, "In Scope (MVP): OTP authentication, client management, workout gallery, training plans, workout logging");
        AddBullet(body, "Out of Scope (Future): Group chats, voice/video calls, wearables integration");

        AddHeading(body, "1.2 Target Users", 3);
        AddParagraph(body, "Primary Users:");
        AddBullet(body, "Trainer: Gym trainers, personal trainers who manage multiple clients");
        AddBullet(body, "Client: Fitness enthusiasts, gym members following fitness programs");
        AddParagraph(body, "User Personas:");
        AddBullet(body, "Trainer Persona:");
        AddBullet(body, "Age: 25-45");
        AddBullet(body, "Tech-savvy, manages 10-50 clients");
        AddBullet(body, "Needs efficient client management tools");
        AddBullet(body, "Client Persona:");
        AddBullet(body, "Age: 18-60");
        AddBullet(body, "Mobile-first user");
        AddBullet(body, "Values simplicity and ease of logging");

        AddHeading(body, "2. FUNCTIONAL REQUIREMENTS", 2);
        AddHeading(body, "2.1 Core Features", 3);
        AddHeading(body, "Feature 1: OTP Authentication", 4);
        AddParagraph(body, "As a user");
        AddParagraph(body, "I want to authenticate via mobile OTP");
        AddParagraph(body, "So that I don't need to remember passwords");
        AddParagraph(body, "Priority: High");
        AddParagraph(body, "Effort: 8 story points");

        AddHeading(body, "Feature 2: Client Profile Creation", 4);
        AddParagraph(body, "As a trainer");
        AddParagraph(body, "I want to create client profiles");
        AddParagraph(body, "So that I can manage client information");
        AddParagraph(body, "Priority: High");
        AddParagraph(body, "Effort: 5 story points");

        AddHeading(body, "Feature 3: Workout Gallery", 4);
        AddParagraph(body, "As a trainer");
        AddParagraph(body, "I want to create and manage workouts");
        AddParagraph(body, "So that I can build workout plans");
        AddParagraph(body, "Priority: High");
        AddParagraph(body, "Effort: 8 story points");

        AddHeading(body, "2.2 User Workflows", 3);
        AddHeading(body, "2.2.1 Trainer Workflow: Client Onboarding", 4);
        AddNumberedItem(body, "Trainer logs in via OTP");
        AddNumberedItem(body, "Trainer navigates to `"Add Client`"");
        AddNumberedItem(body, "Trainer fills client form (name, phone, height, weight, goal)");
        AddNumberedItem(body, "System creates client profile and generates invite code");
        AddNumberedItem(body, "System sends invite SMS to client phone number");

        AddHeading(body, "2.2.2 Trainer Workflow: Assign Training Plan", 4);
        AddNumberedItem(body, "Trainer selects client from client list");
        AddNumberedItem(body, "Trainer navigates to `"Workouts`" tab");
        AddNumberedItem(body, "Trainer clicks `"Assign Plan`"");
        AddNumberedItem(body, "Trainer builds weekly plan: Select day → Add workout → Configure sets/reps/weight");
        AddNumberedItem(body, "Trainer sets start/end dates and adds notes");
        AddNumberedItem(body, "Trainer saves plan");

        AddHeading(body, "2.3 Business Rules", 3);
        AddHeading(body, "Business Rule 1: OTP Rules", 4);
        AddBullet(body, "OTP valid for 10 minutes");
        AddBullet(body, "Maximum 3 OTP attempts per phone number per hour");
        AddBullet(body, "Account temporarily blocked after 3 failed attempts (15-minute lockout)");

        AddHeading(body, "Business Rule 2: Client Management Rules", 4);
        AddBullet(body, "Phone number must be unique per trainer");
        AddBullet(body, "Same phone number can exist for different trainers (different clients)");
        AddBullet(body, "Invite code valid for 30 days");

        AddHeading(body, "3. USER STORIES", 2);
        AddHeading(body, "3.1 Epic Stories", 3);
        AddHeading(body, "Epic 1: User Authentication & Onboarding", 4);
        AddParagraph(body, "As a trainer or client");
        AddParagraph(body, "I want to authenticate using my mobile number and OTP");
        AddParagraph(body, "So that I can securely access the platform without managing passwords");

        AddHeading(body, "Epic 2: Client Management", 4);
        AddParagraph(body, "As a trainer");
        AddParagraph(body, "I want to manage my clients' profiles and information");
        AddParagraph(body, "So that I can track their progress and provide personalized guidance");

        AddHeading(body, "Epic 3: Workout Plan Management", 4);
        AddParagraph(body, "As a trainer");
        AddParagraph(body, "I want to create and assign workout plans to clients");
        AddParagraph(body, "So that my clients can follow structured training programs");

        AddHeading(body, "4. TECHNICAL SPECIFICATIONS", 2);
        AddHeading(body, "4.1 System Architecture", 3);
        AddParagraph(body, "The system uses a microservices architecture with:");
        AddBullet(body, "Frontend: React PWA");
        AddBullet(body, "Backend: Node.js with NestJS");
        AddBullet(body, "Database: MongoDB Atlas");
        AddBullet(body, "Storage: AWS S3 for media files");

        AddHeading(body, "4.2 API Specifications", 3);
        AddHeading(body, "Endpoint 1: POST /api/auth/request-otp", 4);
        AddParagraph(body, "Request OTP for mobile number authentication.");

        AddHeading(body, "Endpoint 2: POST /api/auth/verify-otp", 4);
        AddParagraph(body, "Verify OTP and create JWT session.");

        AddHeading(body, "Endpoint 3: POST /api/trainers/:id/clients", 4);
        AddParagraph(body, "Create new client profile.");

        AddHeading(body, "5. DATA REQUIREMENTS", 2);
        AddHeading(body, "5.1 Data Entities", 3);
        AddHeading(body, "Data Entity 1: User", 4);
        AddParagraph(body, "id, role, name, mobile_number, last_login, profile_photo, created_at, updated_at");

        AddHeading(body, "Data Entity 2: ClientProfile", 4);
        AddParagraph(body, "user_id, trainer_id, dob, gender, height_cm, initial_weight_kg, goals, notes, created_at, updated_at");

        AddHeading(body, "5.2 Data Relationships", 3);
        AddBullet(body, "User → TrainerProfile (1:1)");
        AddBullet(body, "User → ClientProfile (1:1)");
        AddBullet(body, "Trainer → ClientProfile (1:many)");

        AddHeading(body, "6. INTEGRATION REQUIREMENTS", 2);
        AddHeading(body, "Integration 1: SMS Provider (Twilio)", 4);
        AddParagraph(body, "Integration with Twilio for OTP delivery via SMS.");

        AddHeading(body, "Integration 2: AWS S3", 4);
        AddParagraph(body, "Integration with AWS S3 for media file storage and CDN delivery.");

        AddHeading(body, "7. REFERENCES", 2);
        AddNumberedItem(body, "React Documentation: https://react.dev");
        AddNumberedItem(body, "NestJS Documentation: https://docs.nestjs.com");
        AddNumberedItem(body, "MongoDB Atlas: https://www.mongodb.com/cloud/atlas");

        mainPart.Document.Save();
    }

    public static void CreateTemplateDotx(string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Template);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var body = mainPart.Document.Body;

        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = CreateStyles();

        AddHeading(body, "Functional Technical Specification Document (FTSD)", 1);
        AddEmptyLine(body);

        AddParagraph(body, "Project Name: [PROJECT_NAME]");
        AddParagraph(body, "Version: [VERSION_NUMBER]");
        AddParagraph(body, "Date: [DATE]");
        AddParagraph(body, "Author(s): [AUTHOR_NAME(S)]");
        AddParagraph(body, "Reviewer(s): [REVIEWER_NAME(S)]");
        AddParagraph(body, "Approver(s): [APPROVER_NAME(S)]");
        AddParagraph(body, "Status: [STATUS]");
        AddEmptyLine(body);

        AddHeading(body, "Table of Contents", 2);
        AddEmptyLine(body);

        AddHeading(body, "1. Document Information", 2);
        AddHeading(body, "1.1 Document Control", 3);
        AddHeading(body, "Version History", 4);
        AddParagraph(body, "Version: [Date]");
        AddParagraph(body, "Author: [Author]");
        AddParagraph(body, "Changes: [Changes]");
        AddParagraph(body, "Approved By: [Approver]");

        AddHeading(body, "Change Log", 4);
        AddParagraph(body, "Date: [Date]");
        AddParagraph(body, "Author: [Author]");
        AddParagraph(body, "Changes: [Changes]");

        AddHeading(body, "1.2 Purpose and Scope", 3);
        AddParagraph(body, "[Provide a high-level overview of the project, including objectives and scope]");

        AddHeading(body, "1.3 Document Conventions", 3);
        AddParagraph(body, "This document follows standard technical specification format.");

        AddHeading(body, "1.4 References", 3);
        AddParagraph(body, "[References]");

        AddHeading(body, "2. Executive Summary", 2);
        AddHeading(body, "2.1 Overview", 3);
        AddParagraph(body, "[Provide a high-level overview of the project, including objectives and scope]");

        AddHeading(body, "2.2 Key Objectives", 3);
        AddParagraph(body, "[Project Objectives]");

        AddHeading(body, "2.3 Business Context", 3);
        AddParagraph(body, "[Business Context]");

        AddHeading(body, "2.4 Success Metrics", 3);
        AddParagraph(body, "[Success Metrics]");

        AddHeading(body, "3. Project Overview", 2);
        AddHeading(body, "3.1 Project Description", 3);
        AddParagraph(body, "[Project Description]");

        AddHeading(body, "3.2 Project Objectives", 3);
        AddParagraph(body, "[Project Objectives]");

        AddHeading(body, "3.3 Target Users and Personas", 3);
        AddParagraph(body, "Persona 1: [Persona Name]");
        AddParagraph(body, "Persona 2: [Persona Name]");

        AddHeading(body, "3.4 Business Goals", 3);
        AddParagraph(body, "[Business Goals]");

        AddHeading(body, "3.5 Scope and Boundaries", 3);
        AddParagraph(body, "[Scope]");

        AddHeading(body, "4. Functional Requirements", 2);
        AddHeading(body, "4.1 Core Features", 3);
        AddParagraph(body, "Feature 1: [Feature Name]");
        AddParagraph(body, "Feature 2: [Feature Name]");

        AddHeading(body, "4.2 User Workflows", 3);
        AddParagraph(body, "Workflow 1: [Workflow Name]");
        AddParagraph(body, "Workflow 2: [Workflow Name]");

        AddHeading(body, "4.3 Business Rules", 3);
        AddParagraph(body, "Business Rule 1: [Rule Name]");
        AddParagraph(body, "Business Rule 2: [Rule Name]");

        AddHeading(body, "4.4 Data Requirements", 3);
        AddParagraph(body, "Data Entity 1: [Entity Name]");
        AddParagraph(body, "Data Entity 2: [Entity Name]");

        AddHeading(body, "4.5 Integration Requirements", 3);
        AddParagraph(body, "Integration 1: [Integration Name]");

        AddHeading(body, "5. User Stories", 2);
        AddHeading(body, "5.1 Epic-Level User Stories", 3);
        AddParagraph(body, "Epic 1: [Epic Name]");
        AddParagraph(body, "Epic 2: [Epic Name]");

        AddHeading(body, "5.2 Feature-Level User Stories", 3);
        AddParagraph(body, "User Story 1: [Story Title]");
        AddParagraph(body, "User Story 2: [Story Title]");

        AddHeading(body, "5.3 Acceptance Criteria", 3);
        AddParagraph(body, "[Acceptance Criteria]");

        AddHeading(body, "6. Technical Specifications", 2);
        AddHeading(body, "6.1 System Architecture", 3);
        AddHeading(body, "Architecture Overview", 4);
        AddParagraph(body, "[Architecture Overview]");

        AddHeading(body, "Component Architecture", 4);
        AddParagraph(body, "[Component Architecture]");

        AddHeading(body, "Deployment Architecture", 4);
        AddParagraph(body, "[Deployment Architecture]");

        AddHeading(body, "6.2 Technology Stack", 3);
        AddHeading(body, "Backend Technologies", 4);
        AddParagraph(body, "[Backend Technologies]");

        AddHeading(body, "Frontend Technologies", 4);
        AddParagraph(body, "[Frontend Technologies]");

        AddHeading(body, "Database Technologies", 4);
        AddParagraph(body, "[Database Technologies]");

        AddHeading(body, "Authentication & Authorization", 4);
        AddParagraph(body, "[Authentication & Authorization]");

        AddHeading(body, "Deployment & Infrastructure", 4);
        AddParagraph(body, "[Deployment & Infrastructure]");

        AddHeading(body, "6.3 API Specifications", 3);
        AddHeading(body, "API Overview", 4);
        AddParagraph(body, "[API Overview]");

        AddParagraph(body, "Endpoint 1: [Endpoint Name]");
        AddParagraph(body, "Endpoint 2: [Endpoint Name]");

        AddHeading(body, "6.4 Database Design", 3);
        AddHeading(body, "Database Schema Overview", 4);
        AddParagraph(body, "[Database Schema Overview]");

        AddParagraph(body, "Entity 1: [Entity Name]");
        AddParagraph(body, "Entity 2: [Entity Name]");

        AddHeading(body, "Relationships", 4);
        AddParagraph(body, "[Relationships]");

        AddHeading(body, "6.5 Security Requirements", 3);
        AddHeading(body, "Authentication", 4);
        AddParagraph(body, "[Authentication]");

        AddHeading(body, "Authorization", 4);
        AddParagraph(body, "[Authorization]");

        AddHeading(body, "Data Protection", 4);
        AddParagraph(body, "[Data Protection]");

        AddHeading(body, "6.6 Performance Requirements", 3);
        AddParagraph(body, "[Performance Requirements]");

        AddHeading(body, "7. Appendices", 2);
        AddHeading(body, "7.1 Glossary", 3);
        AddParagraph(body, "[Glossary]");

        AddHeading(body, "7.2 References", 3);
        AddParagraph(body, "[References]");

        AddHeading(body, "7.3 Assumptions", 3);
        AddParagraph(body, "[Assumptions]");

        AddHeading(body, "7.4 Risks", 3);
        AddParagraph(body, "[Risks]");

        mainPart.Document.Save();
    }

    private static Styles CreateStyles()
    {
        var styles = new Styles();
        var defaultStyle = new Style()
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true
        };
        defaultStyle.Append(new StyleName() { Val = "Normal" });
        defaultStyle.Append(new NextParagraphStyle() { Val = "Normal" });
        defaultStyle.Append(new StyleRunProperties(
            new RunFonts() { Ascii = "Calibri", HighAnsi = "Calibri" },
            new FontSize() { Val = "22" }
        ));
        styles.Append(defaultStyle);

        for (int i = 1; i <= 4; i++)
        {
            var headingStyle = new Style()
            {
                Type = StyleValues.Paragraph,
                StyleId = $"Heading{i}"
            };
            headingStyle.Append(new StyleName() { Val = $"Heading {i}" });
            headingStyle.Append(new NextParagraphStyle() { Val = "Normal" });
            var runProps = new StyleRunProperties(
                new RunFonts() { Ascii = "Calibri", HighAnsi = "Calibri" },
                new Bold(),
                new FontSize() { Val = ((44 - (i * 2)) * 2).ToString() }
            );
            headingStyle.Append(runProps);
            headingStyle.Append(new StyleParagraphProperties(
                new SpacingBetweenLines() { After = "120" }
            ));
            styles.Append(headingStyle);
        }

        return styles;
    }

    private static void AddHeading(Body body, string text, int level)
    {
        var para = new Paragraph();
        para.ParagraphProperties = new ParagraphProperties(
            new ParagraphStyleId() { Val = $"Heading{level}" }
        );
        para.Append(new Run(new Text(text)));
        body.Append(para);
    }

    private static void AddParagraph(Body body, string text)
    {
        var para = new Paragraph(new Run(new Text(text)));
        body.Append(para);
    }

    private static void AddEmptyLine(Body body)
    {
        body.Append(new Paragraph(new Run(new Text(""))));
    }

    private static void AddBullet(Body body, string text)
    {
        var para = new Paragraph();
        para.ParagraphProperties = new ParagraphProperties(
            new NumberingProperties(
                new NumberingLevelReference() { Val = 0 },
                new NumberingId() { Val = 1 }
            )
        );
        para.Append(new Run(new Text(text)));
        body.Append(para);
    }

    private static void AddNumberedItem(Body body, string text)
    {
        var para = new Paragraph();
        para.ParagraphProperties = new ParagraphProperties(
            new NumberingProperties(
                new NumberingLevelReference() { Val = 0 },
                new NumberingId() { Val = 2 }
            )
        );
        para.Append(new Run(new Text(text)));
        body.Append(para);
    }
}
"@

# Compile and run the code
$assemblyPath = "$testFilesDir\WordGenerator.dll"
$sourceFile = "$testFilesDir\WordGenerator.cs"

# Write the C# code to a file
$csharpCode | Out-File -FilePath $sourceFile -Encoding UTF8

# Add required using statements and compile
$fullCode = @"
using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

$csharpCode
"@

$fullCode | Out-File -FilePath $sourceFile -Encoding UTF8

Write-Host "Note: This PowerShell script requires .NET SDK and DocumentFormat.OpenXml package."
Write-Host "For a simpler approach, please use the C# files in the TestFiles directory and compile them separately."
Write-Host ""
Write-Host "Alternatively, you can manually create the Word documents using the markdown files as reference:"
Write-Host "  - example_proper_raw.md -> example_proper_raw.docx"
Write-Host "  - example_proper_template.md -> example_proper_template.dotx"


