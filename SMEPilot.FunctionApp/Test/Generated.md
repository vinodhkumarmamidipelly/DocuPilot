FUNCTIONAL SPECIFICATION DOCUMENT (FSD) 

 

Project Name: 

FitNex Fitness App - Trainer & Client Management Platform 

Version: 

1.0 

Date: 

2024-12-01 

Author(s): 

[AUTHOR_NAME] 

Reviewer(s): 

[REVIEWER_NAME] 

Approver(s): 

[APPROVER_NAME] 

Status: 

REVIEW 

 

 

Table of Contents 

1. FUNCTIONAL SPECIFICATION DOCUMENT (FSD) 

2. FitNex Fitness App - MVP 

3. 1. PROJECT OVERVIEW 

4. 1.1 Project Information 

5. - Project Name: FitNex Fitness App 

6. - Project Objectives: 

7. - Project Scope: 

8. 1.2 Target Users 

9. - Primary Users: 

10. - Secondary Users: 

11. - User Personas: 

12. - Trainer Persona: 

13. - Client Persona: 

14. - User Goals: 

15. 2. FUNCTIONAL REQUIREMENTS 

16. 2.1 Core Features 

17. 2.1.1 Authentication & Authorization 

18. - Mobile Number + OTP Authentication 

19. - Role-Based Access Control 

20. 2.1.2 Client Management 

21. - Create/Edit/Delete Client Profiles 

22. 2.1.3 Workout Gallery & Media 

23. - Workout Management 

24. 2.1.4 Training Plans 

25. - Weekly Plan Builder 

26. 2.1.5 Workout Logging 

27. - Exercise Logging 

28. 3. USER STORIES 

29. 3.1 Epic Stories 

30. Epic 1: User Authentication & Onboarding 

31. Epic 2: Client Management 

32. Epic 3: Workout Plan Management 

33. Epic 4: Workout Logging 

34. 3.2 Feature Stories 

35. Feature 1: OTP Authentication 

36. Feature 2: Client Profile Creation 

37. Feature 3: Workout Gallery 

38. 9. APPENDICES 

39. 9.1 Glossary 

40. - PII: Personally Identifiable Information 

41. - RBAC: Role-Based Access Control 

42. - API: Application Programming Interface 

43. - SDK: Software Development Kit 

44. 9.2 References 

45. DOCUMENT VERSION HISTORY 

46. End of Functional Specification Document 

 

 

 

 

1. Document Information 

1.1 Document Control 

Document ID: 

2F0782CB60DA43B7 

Document Type: 

Generic 

Classification: 

Generic 

 

Version History 

Version 

Date 

Author 

Changes 

Approved By 

1.0 

2024-12-01 

Version: 1.0 Date: December 2024 Status: Draft for Review Project: FitNex Fitness App - Trainer & Client Management Platform 

Initial Version 

[APPROVER] 

Change Log 

Change # 

Date 

Section 

Description 

Author 

 

 

 

 

 

 

- Project Name: FitNex Fitness App 

- Project Description: A mobile-first Progressive Web App (PWA) that enables gym trainers to manage clients (profiles, measurements, diet plans, training plans), share workout videos, receive client meal & workout logs (with photos), chat 1:1 with clients, and track progress with charts and simple reports. 

- Project Objectives: 

  - Enable trainers to efficiently manage multiple clients   - Provide clients with easy-to-use workout and meal logging tools   - Facilitate real-time communication between trainers and clients   - Track and visualize fitness progress over time   - Support mobile-first experience with offline capabilities 

- Project Scope: 

  - In Scope (MVP): OTP authentication, client management, workout gallery, training plans, workout logging, measurements, diet plans, meal tracking, 1:1 chat, progress reports   - Out of Scope (Future): Group chats, voice/video calls, calorie/macros auto-calculation, wearables integration, native mobile apps (PWA first) 

- Primary Users: 

  - Trainer: Gym trainers, personal trainers, fitness coaches who manage multiple clients   - Client: Fitness enthusiasts, gym members, individuals following fitness programs 

- Secondary Users: 

  - Admin (optional, future): Platform administrators for content moderation, billing, configuration 

  - Trainer Persona: 

    - Age: 25-45     - Tech-savvy, manages 10-50 clients     - Needs efficient client management tools     - Values quick client progress tracking 

  - Client Persona: 

    - Age: 18-60     - Mobile-first user     - Values simplicity and ease of logging     - Wants clear progress visualization 

- User Goals: 

  - Trainer: Efficiently manage clients, create personalized plans, track progress, communicate effectively   - Client: Follow workout plans, log activities easily, view progress, communicate with trainer 

- Mobile Number + OTP Authentication 

  - User enters mobile number   - System sends OTP via SMS provider (Twilio/MessageBird)   - User enters 6-digit OTP   - System verifies OTP and creates JWT session   - Support for refresh tokens and secure session cookies   - Rate limiting: Max 3 OTP attempts per phone number per hour   - Session management: Support for multiple devices with logout across devices 

- Role-Based Access Control 

  - Trainer Role: Full access to client management, workout gallery, plans, measurements, reports   - Client Role: Access to assigned plans, workout logging, meal logging, progress viewing, chat   - Admin Role (future): System administration, content moderation, billing 

- Create/Edit/Delete Client Profiles 

  - Required fields: Name, Phone Number, Height (cm), Weight (kg), Fitness Goal   - Optional fields: Email, Date of Birth, Gender, Notes, Profile Photo   - Trainer can invite clients by phone number (OTP link flow)   - Client can sign up and link to trainer via invite code   - Duplicate phone number validation   - Client profile includes: Basic info, measurements history, assigned plans, progress metrics 

- Workout Management 

  - Create/edit workouts with: Title, Description, Steps, Benefits, Target Body Parts, Difficulty Level, Tags   - Attach workout video via hosted URL (VOD provider or signed S3 upload)   - Search & filter by: Body part, difficulty, trainer, tags   - Video metadata: Duration, thumbnail, transcoding status   - Workout library: Trainer-specific workouts + shared library 

- Weekly Plan Builder 

  - Day-by-day workout assignment (Monday through Sunday)   - Each day can have multiple workout slots   - Each workout slot contains: Exercise, Sets, Reps, Weight, Rest time, Notes   - Assign plan to client with: Start date, End date, Notes   - Client calendar/week view shows scheduled workouts   - Plan overlap detection and warnings   - Plan templates: Reusable plan templates 

- Exercise Logging 

  - Per-exercise logs: Date, Exercise ID, Sets array (reps, weight, notes), Completion status   - Support for edit history (audit trail)   - Trainer notes on workout logs   - Retain full history for progression reporting   - Quick logging: Reuse previous sets, quick add sets   - Table logger: Bulk logging interface for trainers 

Epic 1: User Authentication & Onboarding 

As a trainer or client I want to authenticate using my mobile number and OTP So that I can securely access the platform without managing passwords Acceptance Criteria: - User can enter mobile number and receive OTP via SMS - User can verify OTP and access the platform - System handles invalid OTP attempts with rate limiting - User can select role (Trainer/Client) after authentication - Session persists across app restarts 

Epic 2: Client Management 

As a trainer I want to manage my clients’ profiles and information So that I can track their progress and provide personalized guidance Acceptance Criteria: - Trainer can create new client profiles with required information - Trainer can edit client information - Trainer can delete clients (with confirmation) - Trainer can invite clients via SMS - Client can link to trainer via invite code 

Epic 3: Workout Plan Management 

As a trainer I want to create and assign workout plans to clients So that my clients can follow structured training programs Acceptance Criteria: - Trainer can create workouts with video and descriptions - Trainer can build weekly training plans with exercises - Trainer can assign plans to clients with dates - System validates plan dates and detects overlaps - Client receives notification when plan is assigned 

Epic 4: Workout Logging 

As a client I want to log my workout sessions So that my trainer can track my progress and provide feedback Acceptance Criteria: - Client can view assigned workouts from plans - Client can log sets, reps, and weight for each exercise - Client can complete workouts and save logs - Trainer can view all client workout logs - System tracks workout history and calculates progress 

Feature 1: OTP Authentication 

As a user I want to authenticate via mobile OTP So that I don’t need to remember passwords Priority: High Effort: 8 story points Acceptance Criteria: - Given a valid mobile number, when user requests OTP, then SMS is sent within 5 seconds - Given OTP is sent, when user enters correct OTP within 10 minutes, then user is authenticated - Given incorrect OTP, when user attempts 3 times, then account is temporarily locked - Given authenticated user, when user refreshes page, then session persists 

Feature 2: Client Profile Creation 

As a trainer I want to create client profiles So that I can manage client information Priority: High Effort: 5 story points Acceptance Criteria: - Given trainer is authenticated, when trainer fills client form, then client profile is created - Given duplicate phone number, when trainer tries to create client, then error is shown - Given client is created, when trainer views client list, then new client appears - Given client profile, when trainer edits information, then changes are saved 

Feature 3: Workout Gallery 

As a trainer I want to create and manage workouts So that I can build workout plans Priority: High Effort: 8 story points Acceptance Criteria: - Given trainer is authenticated, when trainer creates workout, then workout is saved to gallery - Given workout has video, when trainer uploads video, then video is processed and stored - Given workout gallery, when trainer searches by body part, then filtered results are shown - Given workout, when trainer edits details, then changes are saved Image2. 

9.1 Glossary 

- OTP: One-Time Password, a temporary authentication code - PWA: Progressive Web App, a web app with native app features - CDN: Content Delivery Network, for fast media delivery - VOD: Video On Demand, video hosting and streaming service - JWT: JSON Web Token, for secure authentication - BMI: Body Mass Index, calculated as weight/(height²) 

9.2 References 

- Requirements Document: fitness_app_for_trainers_clients_requirements_mvp.md - Stage 1 Outputs: Stage1_Requirements_Mermaid/user_answers.json - Codebase Analysis: Stage1_Requirements_Mermaid/codebase_analysis.json - Mermaid Diagrams: Stage1_Requirements_Mermaid/diagrams/*.mmd - Technology Stack: Node.js, NestJS, React, MongoDB Atlas, Redis, AWS S3, CloudFront 

 

 

 

  - Admin (optional, future): Platform administrators for content moderation, billing, configuration 

This document is confidential and proprietary. Unauthorized distribution is prohibited. 