FUNCTIONAL SPECIFICATION DOCUMENT (FSD)
FitNex Fitness App - MVP
Version: 1.0
Date: December 2024
Status: Draft for Review
Project: FitNex Fitness App - Trainer & Client Management Platform
________________________________________
1. PROJECT OVERVIEW
1.1 Project Information
•	Project Name: FitNex Fitness App
•	Project Description: A mobile-first Progressive Web App (PWA) that enables gym trainers to manage clients (profiles, measurements, diet plans, training plans), share workout videos, receive client meal & workout logs (with photos), chat 1:1 with clients, and track progress with charts and simple reports.
•	Project Objectives:
–	Enable trainers to efficiently manage multiple clients
–	Provide clients with easy-to-use workout and meal logging tools
–	Facilitate real-time communication between trainers and clients
–	Track and visualize fitness progress over time
–	Support mobile-first experience with offline capabilities
•	Project Scope:
–	In Scope (MVP): OTP authentication, client management, workout gallery, training plans, workout logging, measurements, diet plans, meal tracking, 1:1 chat, progress reports
–	Out of Scope (Future): Group chats, voice/video calls, calorie/macros auto-calculation, wearables integration, native mobile apps (PWA first)
1.2 Target Users
•	Primary Users:
–	Trainer: Gym trainers, personal trainers, fitness coaches who manage multiple clients
–	Client: Fitness enthusiasts, gym members, individuals following fitness programs
•	Secondary Users:
–	Admin (optional, future): Platform administrators for content moderation, billing, configuration
•	User Personas:
–	Trainer Persona:
•	Age: 25-45
•	Tech-savvy, manages 10-50 clients
•	Needs efficient client management tools
•	Values quick client progress tracking
–	Client Persona:
•	Age: 18-60
•	Mobile-first user
•	Values simplicity and ease of logging
•	Wants clear progress visualization
•	User Goals:
–	Trainer: Efficiently manage clients, create personalized plans, track progress, communicate effectively
–	Client: Follow workout plans, log activities easily, view progress, communicate with trainer
2. FUNCTIONAL REQUIREMENTS
2.1 Core Features
2.1.1 Authentication & Authorization
•	Mobile Number + OTP Authentication
–	User enters mobile number
–	System sends OTP via SMS provider (Twilio/MessageBird)
–	User enters 6-digit OTP
–	System verifies OTP and creates JWT session
–	Support for refresh tokens and secure session cookies
–	Rate limiting: Max 3 OTP attempts per phone number per hour
–	Session management: Support for multiple devices with logout across devices
•	Role-Based Access Control
–	Trainer Role: Full access to client management, workout gallery, plans, measurements, reports
–	Client Role: Access to assigned plans, workout logging, meal logging, progress viewing, chat
–	Admin Role (future): System administration, content moderation, billing
2.1.2 Client Management
•	Create/Edit/Delete Client Profiles
–	Required fields: Name, Phone Number, Height (cm), Weight (kg), Fitness Goal
–	Optional fields: Email, Date of Birth, Gender, Notes, Profile Photo
–	Trainer can invite clients by phone number (OTP link flow)
–	Client can sign up and link to trainer via invite code
–	Duplicate phone number validation
–	Client profile includes: Basic info, measurements history, assigned plans, progress metrics
2.1.3 Workout Gallery & Media
•	Workout Management
–	Create/edit workouts with: Title, Description, Steps, Benefits, Target Body Parts, Difficulty Level, Tags
–	Attach workout video via hosted URL (VOD provider or signed S3 upload)
–	Search & filter by: Body part, difficulty, trainer, tags
–	Video metadata: Duration, thumbnail, transcoding status
–	Workout library: Trainer-specific workouts + shared library
2.1.4 Training Plans
•	Weekly Plan Builder
–	Day-by-day workout assignment (Monday through Sunday)
–	Each day can have multiple workout slots
–	Each workout slot contains: Exercise, Sets, Reps, Weight, Rest time, Notes
–	Assign plan to client with: Start date, End date, Notes
–	Client calendar/week view shows scheduled workouts
–	Plan overlap detection and warnings
–	Plan templates: Reusable plan templates
2.1.5 Workout Logging
•	Exercise Logging
–	Per-exercise logs: Date, Exercise ID, Sets array (reps, weight, notes), Completion status
–	Support for edit history (audit trail)
–	Trainer notes on workout logs
–	Retain full history for progression reporting
–	Quick logging: Reuse previous sets, quick add sets
–	Table logger: Bulk logging interface for trainers
3. USER STORIES
3.1 Epic Stories
Epic 1: User Authentication & Onboarding
As a trainer or client
I want to authenticate using my mobile number and OTP
So that I can securely access the platform without managing passwords
Acceptance Criteria: - User can enter mobile number and receive OTP via SMS - User can verify OTP and access the platform - System handles invalid OTP attempts with rate limiting - User can select role (Trainer/Client) after authentication - Session persists across app restarts
Epic 2: Client Management
As a trainer
I want to manage my clients’ profiles and information
So that I can track their progress and provide personalized guidance
Acceptance Criteria: - Trainer can create new client profiles with required information - Trainer can edit client information - Trainer can delete clients (with confirmation) - Trainer can invite clients via SMS - Client can link to trainer via invite code
Epic 3: Workout Plan Management
As a trainer
I want to create and assign workout plans to clients
So that my clients can follow structured training programs
Acceptance Criteria: - Trainer can create workouts with video and descriptions - Trainer can build weekly training plans with exercises - Trainer can assign plans to clients with dates - System validates plan dates and detects overlaps - Client receives notification when plan is assigned
Epic 4: Workout Logging
As a client
I want to log my workout sessions
So that my trainer can track my progress and provide feedback
Acceptance Criteria: - Client can view assigned workouts from plans - Client can log sets, reps, and weight for each exercise - Client can complete workouts and save logs - Trainer can view all client workout logs - System tracks workout history and calculates progress
 
3.2 Feature Stories
Feature 1: OTP Authentication
As a user
I want to authenticate via mobile OTP
So that I don’t need to remember passwords
Priority: High
Effort: 8 story points
Acceptance Criteria: - Given a valid mobile number, when user requests OTP, then SMS is sent within 5 seconds - Given OTP is sent, when user enters correct OTP within 10 minutes, then user is authenticated - Given incorrect OTP, when user attempts 3 times, then account is temporarily locked - Given authenticated user, when user refreshes page, then session persists
Feature 2: Client Profile Creation
As a trainer
I want to create client profiles
So that I can manage client information
Priority: High
Effort: 5 story points
Acceptance Criteria: - Given trainer is authenticated, when trainer fills client form, then client profile is created - Given duplicate phone number, when trainer tries to create client, then error is shown - Given client is created, when trainer views client list, then new client appears - Given client profile, when trainer edits information, then changes are saved
Feature 3: Workout Gallery
As a trainer
I want to create and manage workouts
So that I can build workout plans
Priority: High
Effort: 8 story points
Acceptance Criteria: - Given trainer is authenticated, when trainer creates workout, then workout is saved to gallery - Given workout has video, when trainer uploads video, then video is processed and stored - Given workout gallery, when trainer searches by body part, then filtered results are shown - Given workout, when trainer edits details, then changes are saved
  

 
Image2.
–	

9. APPENDICES
9.1 Glossary
•	OTP: One-Time Password, a temporary authentication code
•	PWA: Progressive Web App, a web app with native app features
•	CDN: Content Delivery Network, for fast media delivery
•	VOD: Video On Demand, video hosting and streaming service
•	JWT: JSON Web Token, for secure authentication
•	BMI: Body Mass Index, calculated as weight/(height²)
•	PII: Personally Identifiable Information
•	RBAC: Role-Based Access Control
•	API: Application Programming Interface
•	SDK: Software Development Kit
9.2 References
•	Requirements Document: fitness_app_for_trainers_clients_requirements_mvp.md
•	Stage 1 Outputs: Stage1_Requirements_Mermaid/user_answers.json
•	Codebase Analysis: Stage1_Requirements_Mermaid/codebase_analysis.json
•	Mermaid Diagrams: Stage1_Requirements_Mermaid/diagrams/*.mmd
•	Technology Stack: Node.js, NestJS, React, MongoDB Atlas, Redis, AWS S3, CloudFront
________________________________________
DOCUMENT VERSION HISTORY
Version	Date	Author	Changes
1.0	December 2024	VIBE Framework	Initial FSD creation from Stage 1 outputs
________________________________________
End of Functional Specification Document
