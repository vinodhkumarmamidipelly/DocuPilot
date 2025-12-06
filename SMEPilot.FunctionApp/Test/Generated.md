 

FitNex Fitness App - MVP 

 

Project Name: 

FitNex Fitness App 

Version: 

1.0 

Date: 

Valid date format, not in future for logs 

Author(s): 

Vinod Kumar Mamidipelly 

Reviewer(s): 

 

Approver(s): 

 

Status: 

Draft for Review 

 

 

 
 

 

 

 

1. Document Information 

1.1 Document Control 

Document ID: 

FitNex Fitness App - v1.0 

Document Type: 

FUNCTIONAL SPECIFICATION DOCUMENT (FSD) 

Classification: 

 

 

Version History 

Version 

Date 

Author 

Changes 

Approved By 

1.0 

Valid date format, not in future for logs 

 

Initial Version 

 

1.0 

2025-12-05 15:56:59 UTC 

Vinod Kumar Mamidipelly 

Updated content based on latest raw document 

 

Change Log 

Change # 

Date 

Section 

Description 

Author 

 

 

 

 

 

1 

2025-12-05 15:56:59 UTC 

Content 

Updated sections: Content 

Vinod Kumar Mamidipelly 

 

[Document Content Starts Here] 

 

 

 

[Document Content Ends Here] 

This document is confidential and proprietary. Unauthorized distribution is prohibited. 

FUNCTIONAL SPECIFICATION DOCUMENT (FSD) 

FitNex Fitness App - MVP 

Version: 1.0 
Date: December 2024 
Status: Draft for Review 
Project: FitNex Fitness App - Trainer & Client Management Platform 

Shape 

1. PROJECT OVERVIEW 

1.1 Project Information 

Project Name: FitNex Fitness App 

Project Description: A mobile-first Progressive Web App (PWA) that enables gym trainers to manage clients (profiles, measurements, diet plans, training plans), share workout videos, receive client meal & workout logs (with photos), chat 1:1 with clients, and track progress with charts and simple reports. 

Project Objectives: 

Enable trainers to efficiently manage multiple clients 

Provide clients with easy-to-use workout and meal logging tools 

Facilitate real-time communication between trainers and clients 

Track and visualize fitness progress over time 

Support mobile-first experience with offline capabilities 

Project Scope: 

In Scope (MVP): OTP authentication, client management, workout gallery, training plans, workout logging, measurements, diet plans, meal tracking, 1:1 chat, progress reports 

Out of Scope (Future): Group chats, voice/video calls, calorie/macros auto-calculation, wearables integration, native mobile apps (PWA first) 

1.2 Target Users 

Primary Users: 

Trainer: Gym trainers, personal trainers, fitness coaches who manage multiple clients 

Client: Fitness enthusiasts, gym members, individuals following fitness programs 

Secondary Users: 

Admin (optional, future): Platform administrators for content moderation, billing, configuration 

User Personas: 

Trainer Persona: 

Age: 25-45 

Tech-savvy, manages 10-50 clients 

Needs efficient client management tools 

Values quick client progress tracking 

Client Persona: 

Age: 18-60 

Mobile-first user 

Values simplicity and ease of logging 

Wants clear progress visualization 

User Goals: 

Trainer: Efficiently manage clients, create personalized plans, track progress, communicate effectively 

Client: Follow workout plans, log activities easily, view progress, communicate with trainer 

1.3 Success Metrics 

Key Performance Indicators: 

% trainers who add ≥1 client within 7 days 

Average weekly active clients per trainer 

30-day trainer retention rate 

Average workout logs per client per week 

Diet adherence % (planned meals vs logged meals) 

NPS / Trainer satisfaction score 

Business Metrics: 

User acquisition rate 

Monthly active users (MAU) 

User engagement rate 

Feature adoption rate 

User Metrics: 

User satisfaction score 

Session duration 

Feature usage frequency 

User retention rate 

Technical Metrics: 

API response time (< 500ms p95) 

System uptime (99.5% target) 

Mobile app performance score 

Error rate (< 0.1%) 

2. FUNCTIONAL REQUIREMENTS 

2.1 Core Features 

2.1.1 Authentication & Authorization 

Mobile Number + OTP Authentication 

User enters mobile number 

System sends OTP via SMS provider (Twilio/MessageBird) 

User enters 6-digit OTP 

System verifies OTP and creates JWT session 

Support for refresh tokens and secure session cookies 

Rate limiting: Max 3 OTP attempts per phone number per hour 

Session management: Support for multiple devices with logout across devices 

Role-Based Access Control 

Trainer Role: Full access to client management, workout gallery, plans, measurements, reports 

Client Role: Access to assigned plans, workout logging, meal logging, progress viewing, chat 

Admin Role (future): System administration, content moderation, billing 

2.1.2 Client Management 

Create/Edit/Delete Client Profiles 

Required fields: Name, Phone Number, Height (cm), Weight (kg), Fitness Goal 

Optional fields: Email, Date of Birth, Gender, Notes, Profile Photo 

Trainer can invite clients by phone number (OTP link flow) 

Client can sign up and link to trainer via invite code 

Duplicate phone number validation 

Client profile includes: Basic info, measurements history, assigned plans, progress metrics 

2.1.3 Workout Gallery & Media 

Workout Management 

Create/edit workouts with: Title, Description, Steps, Benefits, Target Body Parts, Difficulty Level, Tags 

Attach workout video via hosted URL (VOD provider or signed S3 upload) 

Search & filter by: Body part, difficulty, trainer, tags 

Video metadata: Duration, thumbnail, transcoding status 

Workout library: Trainer-specific workouts + shared library 

2.1.4 Training Plans 

Weekly Plan Builder 

Day-by-day workout assignment (Monday through Sunday) 

Each day can have multiple workout slots 

Each workout slot contains: Exercise, Sets, Reps, Weight, Rest time, Notes 

Assign plan to client with: Start date, End date, Notes 

Client calendar/week view shows scheduled workouts 

Plan overlap detection and warnings 

Plan templates: Reusable plan templates 

2.1.5 Workout Logging 

Exercise Logging 

Per-exercise logs: Date, Exercise ID, Sets array (reps, weight, notes), Completion status 

Support for edit history (audit trail) 

Trainer notes on workout logs 

Retain full history for progression reporting 

Quick logging: Reuse previous sets, quick add sets 

Table logger: Bulk logging interface for trainers 

2.1.6 Measurements 

Body Measurement Tracking 

Record metrics: Weight, Chest, Waist, Hips, Arms, Thighs, Body Fat %, BMI (auto-calculated) 

Timestamped entries with notes 

Visualization: Line charts showing progression over time 

Delta calculation: Change from previous measurement 

Percentage change calculation 

Date range filtering for charts 

Measurement trends and alerts 

2.1.7 Diet & Meal Tracking 

Diet Plan Creation 

Diet templates: Meals with portion guidance 

Assignable to clients with start/end dates 

Meal structure: Meal name, Portion size, Timing, Nutritional info (optional) 

Meal Logging 

Log meals: Item name, Quantity/portion, Time, Optional photo, Optional manual calories/macros 

Trainer view: All meal logs with ability to comment 

Diet adherence calculation: % planned meals logged vs actual 

Photo upload: Signed S3 upload with CDN delivery 

Meal history: Chronological list with filtering 

2.1.8 Chat 

1:1 Chat Channels 

Real-time messaging between trainer and client 

Message types: Text, Image, Short video links 

Read receipts: Message read status tracking 

Timestamps: Message sent/received timestamps 

Push/web notifications: Real-time notifications 

Media attachments: Securely stored and linked to messages 

Message history: Persistent chat history with pagination 

Online status: User online/offline indicators 

2.1.9 Reports & Analytics 

Progress Reports 

Exercise progression graphs: Weight vs date, Volume over time 

Measurement progression charts: Line charts for all metrics 

Diet adherence summary: % planned meals logged 

Export functionality: CSV export for logs and measurements 

Date range filtering: Custom date ranges for reports 

Comparative analysis: Compare current vs previous periods 

2.1.10 Media & Storage 

Media Management 

Signed uploads to object storage (AWS S3) 

CDN delivery: CloudFront for media delivery 

Video hosting: Optional VOD provider (Mux/Cloudflare Stream) for transcoding & HLS 

Thumbnails: Auto-generated thumbnails for videos and images 

Access-control: Signed URLs for secure media access 

File size limits: 10MB images, 500MB videos 

Supported formats: JPG, PNG, MP4, MOV 

2.2 User Workflows 

2.2.1 Trainer Workflow: Client Onboarding 

Trainer logs in via OTP 

Trainer navigates to “Add Client” 

Trainer fills client form (name, phone, height, weight, goal) 

System creates client profile and generates invite code 

System sends invite SMS to client phone number 

Client receives SMS and can sign up using invite code 

Client profile linked to trainer automatically 

2.2.2 Trainer Workflow: Assign Training Plan 

Trainer selects client from client list 

Trainer navigates to “Workouts” tab 

Trainer clicks “Assign Plan” 

Trainer builds weekly plan: Select day → Add workout → Configure sets/reps/weight 

Trainer sets start/end dates and adds notes 

Trainer saves plan 

System validates plan (no overlaps, valid dates) 

System assigns plan to client and sends notification 

Client receives push notification about new plan 

2.2.3 Client Workflow: Log Workout 

Client logs in via OTP 

Client views “Today’s Workout” on dashboard 

Client clicks “Start Workout” 

Client views exercise list from assigned plan 

For each exercise, client logs sets: Enter reps/weight → Mark set complete 

Client completes all exercises 

Client clicks “Complete Workout” 

System saves workout log with timestamp 

System calculates progress metrics 

Trainer receives notification about completed workout 

2.2.4 Client Workflow: Log Meal 

Client navigates to “Meals” tab 

Client clicks “Log Meal” 

Client enters meal name and quantity 

Client optionally uploads photo (camera or gallery) 

Client clicks “Save Meal” 

System uploads photo to S3 and generates signed URL 

System saves meal log with timestamp 

System checks if meal matches assigned diet plan 

System updates diet adherence percentage 

Trainer receives notification about meal log 

2.2.5 Trainer Workflow: Record Measurements 

Trainer selects client from client list 

Trainer navigates to “Overview” tab 

Trainer clicks “Measurements” button 

Trainer enters measurement values (weight, chest, waist, etc.) 

Trainer clicks “Save Measurement” 

System calculates BMI and delta from previous measurement 

System updates progress charts 

Client receives notification about new measurement 

Charts automatically update with new data point 

2.3 Business Rules 

2.3.1 Authentication Rules 

OTP Rules: 

OTP valid for 10 minutes 

Maximum 3 OTP attempts per phone number per hour 

Account temporarily blocked after 3 failed attempts (15-minute lockout) 

OTP must be 6 digits 

OTP stored in Redis with TTL 

Session Rules: 

JWT access token valid for 24 hours 

Refresh token valid for 7 days 

Multiple sessions allowed per user (different devices) 

Logout invalidates all sessions 

2.3.2 Client Management Rules 

Duplicate Prevention: 

Phone number must be unique per trainer 

Same phone number can exist for different trainers (different clients) 

Invite Code Rules: 

Invite code valid for 30 days 

Invite code is 8-character alphanumeric 

Invite code automatically generated when client created 

One invite code per client 

2.3.3 Training Plan Rules 

Plan Assignment Rules: 

Plan must have valid start date (not in past) 

Plan end date must be after start date 

Plan duration maximum 12 weeks 

Warning if plan overlaps with existing active plan (user confirmation required) 

Client must be linked to trainer before plan assignment 

Workout Plan Rules: 

At least one exercise per workout 

Sets must be > 0 

Reps must be > 0 

Weight must be >= 0 (0 for bodyweight exercises) 

2.3.4 Workout Logging Rules 

Log Validation: 

Workout log must reference valid exercise from assigned plan 

Log date cannot be more than 7 days in future 

Log date cannot be more than 30 days in past 

Sets array must contain at least one set 

Reps must be > 0 

Weight must be >= 0 

Progress Calculation: 

Volume = Sum of (sets × reps × weight) for all exercises 

Progression tracked by comparing current log to previous log for same exercise 

Weekly volume calculated by summing all workout logs for the week 

2.3.5 Measurement Rules 

Validation Rules: 

Weight: 20-300 kg (inclusive) 

Chest: 50-200 cm (inclusive) 

Waist: 40-200 cm (inclusive) 

Hips: 50-200 cm (inclusive) 

Arms: 10-100 cm (inclusive) 

Thighs: 20-150 cm (inclusive) 

Body Fat %: 0-100% (inclusive) 

BMI Calculation: 

BMI = Weight (kg) / (Height (m))² 

BMI categories: Underweight (<18.5), Normal (18.5-24.9), Overweight (25-29.9), Obese (≥30) 

2.3.6 Diet Plan Rules 

Diet Plan Assignment: 

Diet plan must have at least one meal 

Portion sizes must be > 0 

Diet plan can be assigned to multiple clients 

Diet plan can be edited (changes affect all assigned clients) 

Meal Logging Rules: 

Meal name required 

Quantity must be > 0 

Photo optional but recommended 

Timestamp automatically set to current time 

Meal can be logged multiple times per day 

Diet Adherence Calculation: 

Adherence % = (Logged Meals / Planned Meals) × 100 

Time window: Planned meals within 24-hour period 

Threshold: < 70% adherence triggers trainer notification 

2.3.7 Chat Rules 

Message Rules: 

Message length: Maximum 5000 characters 

Media attachments: Maximum 10MB per message 

Rate limiting: Maximum 30 messages per minute per user 

Message retention: Messages stored for 90 days 

Channel Rules: 

1:1 channels only (trainer-client pairs) 

Channel auto-created when first message sent 

Channel persists even if client removed (with archived status) 

2.3.8 Authorization Rules 

Trainer Access: 

Trainer can access only their own clients 

Trainer can create/edit/delete workouts in their gallery 

Trainer can view all logs and measurements for their clients 

Trainer cannot access other trainers’ data 

Client Access: 

Client can access only their own data 

Client can view assigned plans and workouts 

Client can log workouts and meals 

Client can view their own progress charts 

Client can chat only with their assigned trainer 

2.4 Data Requirements 

2.4.1 Data Entities 

User: id, role, name, mobile_number, last_login, profile_photo, created_at, updated_at 

TrainerProfile: user_id, bio, certifications, created_at 

ClientProfile: user_id, trainer_id, dob, gender, height_cm, initial_weight_kg, goals, notes, created_at, updated_at 

Workout: id, trainer_id, title, description, target_body_parts[], video_url, tags[], difficulty, created_at, updated_at 

TrainingPlan: id, trainer_id, client_id, name, start_date, end_date, plan_json (weekly structure), notes, created_at, updated_at 

ExerciseLog: id, client_id, exercise_id, date, sets_json, notes, trainer_notes, created_at, updated_at 

Measurement: id, client_id, date, metrics_json (weight, chest, waist, hips, arms, thighs, body_fat, bmi), notes, created_at 

DietPlan: id, trainer_id, client_id, name, meals_json, start_date, end_date, created_at, updated_at 

MealLog: id, client_id, date_time, items_json (name, quantity, calories, macros), photo_url, trainer_comment, created_at 

Message: id, channel_id, sender_id, content, attachments_json, read_at, created_at 

Media: id, owner_id, url, type, meta (size, format, dimensions), created_at 

2.4.2 Data Relationships 

User → TrainerProfile (1:1) 

User → ClientProfile (1:1) 

Trainer → ClientProfile (1:many) 

Trainer → Workout (1:many) 

Trainer → TrainingPlan (1:many) 

Client → TrainingPlan (1:many) 

Client → ExerciseLog (1:many) 

Client → Measurement (1:many) 

Client → DietPlan (1:many) 

Client → MealLog (1:many) 

Trainer ↔ Client → Message (many:many via channel) 

2.4.3 Data Validation 

Mobile Number: Valid format, country code required, unique per trainer 

Email: Valid email format (optional) 

Height: Positive number, 50-250 cm 

Weight: Positive number, 20-300 kg 

Date: Valid date format, not in future for logs 

Text Fields: Length limits, special character handling 

File Uploads: Type validation, size limits 

2.4.4 Data Security 

Encryption: PII encrypted at rest (AES-256) 

Transport: TLS 1.3 for all data in transit 

Access Control: Role-based access checks on all data access 

Audit Trail: All data modifications logged with user ID and timestamp 

Data Retention: Data retained per GDPR requirements (user deletion request) 

Backup: Daily automated backups with 30-day retention 

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

Epic 5: Progress Tracking 

As a trainer and client 
I want to track and visualize fitness progress 
So that I can monitor improvements and adjust programs 
Acceptance Criteria: - Trainer can record client body measurements - System calculates BMI and progress metrics - Charts display measurement trends over time - Client can view their progress charts - Reports can be exported to CSV 

Epic 6: Diet & Meal Tracking 

As a trainer and client 
I want to manage diet plans and track meals 
So that clients can follow nutrition guidelines 
Acceptance Criteria: - Trainer can create diet plans with meals and portions - Trainer can assign diet plans to clients - Client can log meals with photos - System calculates diet adherence percentage - Trainer can view and comment on meal logs 

Epic 7: Communication 

As a trainer and client 
I want to communicate in real-time 
So that I can ask questions and receive guidance 
Acceptance Criteria: - Trainer and client can exchange messages in 1:1 chat - Messages support text and media attachments - Real-time message delivery via WebSocket - Read receipts show message status - Push notifications for new messages 

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

Feature 4: Training Plan Builder 

As a trainer 
I want to build weekly training plans 
So that I can assign structured programs to clients 
Priority: High 
Effort: 13 story points 
Acceptance Criteria: - Given trainer selects client, when trainer builds plan, then weekly structure is created - Given plan builder, when trainer adds workouts to days, then workouts are assigned - Given plan with dates, when trainer assigns plan, then client receives notification - Given overlapping plans, when trainer assigns plan, then warning is shown 

Feature 5: Workout Logging 

As a client 
I want to log my workout sessions 
So that my trainer can track my progress 
Priority: High 
Effort: 8 story points 
Acceptance Criteria: - Given client has assigned plan, when client views today’s workout, then exercises are shown - Given workout logger, when client logs sets/reps/weight, then data is saved - Given completed workout, when client saves log, then trainer receives notification - Given workout log, when client views history, then all logs are displayed 

Feature 6: Measurement Tracking 

As a trainer 
I want to record client measurements 
So that I can track client progress 
Priority: Medium 
Effort: 5 story points 
Acceptance Criteria: - Given trainer selects client, when trainer enters measurements, then data is saved - Given measurement saved, when system calculates BMI, then BMI is displayed - Given measurements, when client views charts, then trends are visualized - Given measurements, when trainer exports data, then CSV file is generated 

Feature 7: Meal Logging 

As a client 
I want to log my meals with photos 
So that my trainer can review my nutrition 
Priority: Medium 
Effort: 8 story points 
Acceptance Criteria: - Given client is authenticated, when client logs meal, then meal is saved - Given meal photo, when client uploads photo, then photo is stored securely - Given meal log, when trainer views meals, then all logs are displayed - Given diet plan, when system calculates adherence, then percentage is shown 

Feature 8: Real-time Chat 

As a trainer and client 
I want to chat in real-time 
So that I can communicate instantly 
Priority: High 
Effort: 13 story points 
Acceptance Criteria: - Given trainer and client, when either sends message, then message is delivered instantly - Given message with media, when user attaches file, then file is uploaded and linked - Given message delivered, when recipient reads message, then read receipt is updated - Given offline user, when message is sent, then push notification is triggered 

3.3 User Story Acceptance Criteria Format 

Format: Given-When-Then-And 

Example: OTP Authentication - Given: A user with a valid mobile number - When: User requests OTP - Then: SMS is sent within 5 seconds - And: OTP is stored in Redis with 10-minute TTL - And: User can enter OTP to authenticate 

4. UI/UX REQUIREMENTS 

4.1 User Interface 

Layout Requirements: 

Mobile-first responsive design 

Bottom navigation bar for primary navigation (Home, Workouts, Chat, Profile) 

Top navigation bar with logo, notifications, and user avatar 

Card-based layouts for content display 

Floating action buttons for primary actions (Add Client, Start Workout, Log Meal) 

Navigation Requirements: 

Bottom navigation: Home, Workouts, Chat, Profile (role-specific) 

Trainer-specific: Clients tab in bottom navigation 

Breadcrumb navigation for deep hierarchies 

Back button support for sub-screens 

Tab navigation within screens (Overview, Workouts, Diet, Reports) 

Form Requirements: 

Real-time validation with clear error messages 

Required fields clearly marked with asterisk 

Input masks for phone numbers and dates 

Auto-save for long forms (client creation, plan builder) 

Photo upload with preview and crop functionality 

Display Requirements: 

Progress charts using line graphs (Recharts library) 

Data tables with sorting and filtering 

List views with infinite scroll or pagination 

Card layouts with images and metadata 

Empty states with helpful messages 

4.2 User Experience 

User Journey: See Section 8.1 for complete user journey flow diagram 

Interaction Design: 

Swipe gestures for common actions (swipe to delete) 

Pull-to-refresh for data updates 

Tap animations and loading states 

Haptic feedback for mobile devices 

Smooth transitions between screens 

Accessibility: 

WCAG AA compliance (target) 

Screen reader support (ARIA labels) 

Keyboard navigation support 

High contrast mode support 

Font size scaling support 

Responsive Design: 

Mobile: 320px - 768px (primary focus) 

Tablet: 768px - 1024px 

Desktop: 1024px+ (secondary) 

Breakpoints: sm (640px), md (768px), lg (1024px) 

4.3 Design System 

Color Palette: 

Primary: Existing design system colors (Radix UI + Tailwind CSS) 

Supports light/dark theme modes 

CSS variables for theme customization 

Chart colors: Distinct colors for different metrics 

Typography: 

Font family: System fonts (San Francisco, Roboto, etc.) 

Headings: Bold, clear hierarchy 

Body text: Readable, optimal line height 

Mobile-optimized font sizes 

Components: 

Radix UI component library (50+ components) 

Custom components: ClientCard, WorkoutCard, MealCard, StatCard 

Reusable form components 

Consistent spacing and borders 

Icons and Graphics: 

Lucide React icon library 

Custom fitness-themed icons 

Image placeholders and fallbacks 

Video thumbnails and previews 

5. BUSINESS RULES 

Note: Business rules are detailed in Section 2.3. Key rules summarized below. 

5.1 Business Logic 

Authentication Logic: OTP-based authentication with rate limiting and session management 

Client Management Logic: Unique phone number per trainer, invite code generation and validation 

Plan Assignment Logic: Date validation, overlap detection, duration limits 

Progress Calculation Logic: Volume calculation, progression tracking, BMI calculation 

Diet Adherence Logic: Meal matching, percentage calculation, threshold alerts 

5.2 Constraints 

Technical Constraints: 

MongoDB Atlas free tier limitations 

Media file size limits (10MB images, 500MB videos) 

API rate limiting (30 requests/minute per user) 

Session timeout (24 hours access token, 7 days refresh token) 

Business Constraints: 

Maximum 12 weeks plan duration 

Maximum 30 days past workout log date 

Maximum 7 days future workout log date 

Invite code validity: 30 days 

Regulatory Constraints: 

GDPR compliance for data export and deletion 

PII encryption at rest (AES-256) 

TLS 1.3 for data in transit 

Audit logging for all data modifications 

Performance Constraints: 

API response time < 500ms (p95) 

System uptime target: 99.5% 

Mobile app performance score > 80 

Error rate < 0.1% 

5.3 Exception Handling 

Error Scenarios: 

Network failures: Retry logic with exponential backoff 

Invalid OTP: Clear error message with retry option 

Duplicate phone number: Validation error with suggestion 

Plan overlap: Warning dialog with confirmation 

File upload failure: Error message with retry option 

Error Messages: User-friendly, actionable error messages 

Recovery Procedures: Clear instructions for error resolution 

Fallback Options: Offline mode for viewing cached data, graceful degradation 

6. NON-FUNCTIONAL REQUIREMENTS 

6.1 Performance 

Response Time: 

API endpoints: < 500ms (p95) 

Page load time: < 3 seconds on 3G 

Image loading: < 2 seconds 

Video streaming: Adaptive bitrate 

Throughput: 

Support 1000 concurrent users 

Handle 100 API requests/second 

Process 50 SMS messages/minute 

Scalability: 

Horizontal scaling via load balancer 

Database read replicas for queries 

CDN for media delivery 

Redis caching for session and data 

Availability: 

Target uptime: 99.5% 

Maintenance windows: Scheduled downtime < 4 hours/month 

Failover: Automatic failover to backup systems 

6.2 Security 

Authentication: 

OTP-based authentication via SMS 

JWT tokens with refresh mechanism 

Secure session management 

Multi-device session support 

Authorization: 

Role-based access control (RBAC) 

Resource-level permissions 

API endpoint protection 

Data access validation 

Data Protection: 

PII encryption at rest (AES-256) 

TLS 1.3 for data in transit 

Secure media storage (signed URLs) 

Regular security audits 

Audit Trail: 

Log all authentication events 

Log all data modifications 

Log all access attempts 

Retain logs for 90 days 

6.3 Reliability 

System Uptime: 99.5% availability target 

Error Handling: 

Graceful error handling 

User-friendly error messages 

Automatic retry for transient failures 

Error logging and monitoring 

Backup and Recovery: 

Daily automated backups 

30-day backup retention 

Point-in-time recovery capability 

Disaster recovery plan 

Monitoring: 

Real-time error tracking (Sentry) 

Performance monitoring (Prometheus) 

Dashboard visualization (Grafana) 

CloudWatch logging 

6.4 Usability 

User Experience: 

Intuitive navigation 

Clear visual hierarchy 

Consistent design patterns 

Minimal learning curve 

Learning Curve: 

Onboarding tutorial for new users 

Tooltips for complex features 

Help documentation 

In-app guidance 

Documentation: 

User guide for trainers 

User guide for clients 

API documentation 

Developer documentation 

Training: 

Video tutorials 

Interactive onboarding 

Support resources 

FAQ section 

7. ACCEPTANCE CRITERIA 

7.1 Feature Acceptance Criteria 

Authentication Feature 

User can enter mobile number and receive OTP via SMS within 5 seconds 

User can verify OTP and access the platform 

Invalid OTP attempts are rate-limited (max 3 per hour) 

Session persists across app restarts 

User can logout from all devices 

Client Management Feature 

Trainer can create client profiles with required information 

Duplicate phone numbers are prevented per trainer 

Client can link to trainer via invite code 

Trainer can edit and delete client profiles 

Client list displays all clients with search functionality 

Workout Gallery Feature 

Trainer can create workouts with video and descriptions 

Workouts can be searched and filtered by body part, difficulty 

Video uploads are processed and stored securely 

Workout gallery displays with thumbnails and metadata 

Training Plan Feature 

Trainer can build weekly plans with day-by-day structure 

Plans can be assigned to clients with start/end dates 

Plan overlap detection warns trainer before assignment 

Client receives notification when plan is assigned 

Client can view assigned plans in calendar view 

Workout Logging Feature 

Client can log workouts with sets, reps, and weight 

Workout logs are saved with timestamp 

Trainer can view all client workout logs 

Workout history displays progression over time 

Workout completion triggers trainer notification 

Measurement Feature 

Trainer can record client body measurements 

System calculates BMI automatically 

Measurement charts display trends over time 

Measurement data can be exported to CSV 

Client can view their measurement progress 

Meal Logging Feature 

Client can log meals with photos 

Meal photos are uploaded securely 

Trainer can view and comment on meal logs 

Diet adherence percentage is calculated 

Meal logs display chronologically 

Chat Feature 

Trainer and client can exchange messages in real-time 

Messages support text and media attachments 

Read receipts show message status 

Push notifications for new messages 

Message history persists and loads correctly 

7.2 User Story Acceptance Criteria 

See Section 3.3 for detailed Given-When-Then-And format acceptance criteria 

7.3 System Acceptance Criteria 

Performance: 

API response time < 500ms (p95) 

Page load time < 3 seconds on 3G 

System handles 1000 concurrent users 

Security: 

All API endpoints require authentication 

PII encrypted at rest 

TLS 1.3 for all communications 

Regular security audits passed 

Integration: 

SMS provider integration (Twilio/MessageBird) 

S3 media storage integration 

CDN integration (CloudFront) 

WebSocket real-time communication 

Deployment: 

Successful deployment to staging environment 

Successful deployment to production environment 

CI/CD pipeline functioning correctly 

Monitoring and logging operational 

7.4 Quality Gates 

Code Quality: 

Code coverage > 80% 

No critical security vulnerabilities 

Linting errors resolved 

TypeScript type safety maintained 

Testing: 

Unit tests for all business logic 

Integration tests for API endpoints 

E2E tests for critical user flows 

Performance tests passed 

Documentation: 

API documentation complete 

User guides available 

Developer documentation updated 

README files updated 

User Acceptance: 

User acceptance testing passed 

Stakeholder approval received 

Production readiness confirmed 

8. MERMAID DIAGRAMS 

 

9. APPENDICES 

9.1 Glossary 

OTP: One-Time Password, a temporary authentication code 

PWA: Progressive Web App, a web app with native app features 

CDN: Content Delivery Network, for fast media delivery 

VOD: Video On Demand, video hosting and streaming service 

JWT: JSON Web Token, for secure authentication 

BMI: Body Mass Index, calculated as weight/(height²) 

PII: Personally Identifiable Information 

RBAC: Role-Based Access Control 

API: Application Programming Interface 

SDK: Software Development Kit 

9.2 References 

Requirements Document: fitness_app_for_trainers_clients_requirements_mvp.md 

Stage 1 Outputs: Stage1_Requirements_Mermaid/user_answers.json 

Codebase Analysis: Stage1_Requirements_Mermaid/codebase_analysis.json 

Mermaid Diagrams: Stage1_Requirements_Mermaid/diagrams/*.mmd 

Technology Stack: Node.js, NestJS, React, MongoDB Atlas, Redis, AWS S3, CloudFront 

9.3 Assumptions 

SMS Provider: Twilio or MessageBird will be used for OTP delivery 

Database: MongoDB Atlas free tier will be sufficient for MVP 

Media Storage: AWS S3 will be used for media storage 

CDN: CloudFront will be used for media delivery 

Deployment: Vercel for frontend, AWS for backend 

Monitoring: Sentry for error tracking, Prometheus for metrics 

Compliance: GDPR compliance required for EU users 

Mobile Strategy: PWA first, Capacitor for native app wrapping 

9.4 Risks 

SMS Cost & Delivery: 

Risk: High SMS costs, delivery failures 

Mitigation: Use reliable provider (Twilio), implement rate limits, fallback flows 

Video Storage Costs: 

Risk: High storage and bandwidth costs 

Mitigation: Use CDN, consider VOD provider, optimize video formats 

Privacy/Regulatory: 

Risk: Data privacy violations, regulatory non-compliance 

Mitigation: Encrypt PII, provide data export/deletion, explicit consent 

Real-time Scaling: 

Risk: WebSocket connection limits, scaling issues 

Mitigation: Use managed services (Pusher/Ably) or scalable Socket.IO + Redis 

Database Performance: 

Risk: MongoDB Atlas free tier limitations 

Mitigation: Optimize queries, use indexes, plan for upgrade 

Mobile App Performance: 

Risk: Slow performance on low-end devices 

Mitigation: Optimize bundle size, lazy loading, CDN for assets 

Shape 

DOCUMENT VERSION HISTORY 

Version 

Date 

Author 

Changes 

1.0 

December 2024 

VIBE Framework 

Initial FSD creation from Stage 1 outputs 

Shape 

End of Functional Specification Document 