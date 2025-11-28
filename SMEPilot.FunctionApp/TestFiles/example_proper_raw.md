# FUNCTIONAL SPECIFICATION DOCUMENT (FSD)

## Project Name: Sample Fitness App

**Version:** 1.0
**Date:** December 2024
**Status:** Draft for Review
**Project:** Sample Fitness App - Trainer & Client Management Platform

## 1. PROJECT OVERVIEW

### 1.1 Project Information

-   **Project Name**: Sample Fitness App
-   **Project Description**: A mobile-first Progressive Web App (PWA) that enables gym trainers to manage clients, share workout videos, and track progress.
-   **Project Objectives**:
    -   Enable trainers to efficiently manage multiple clients
    -   Provide clients with easy-to-use workout logging tools
    -   Track and visualize fitness progress over time
-   **Project Scope**:
    -   **In Scope (MVP)**: OTP authentication, client management, workout gallery, training plans, workout logging
    -   **Out of Scope (Future)**: Group chats, voice/video calls, wearables integration

### 1.2 Target Users

-   **Primary Users**:
    -   **Trainer**: Gym trainers, personal trainers who manage multiple clients
    -   **Client**: Fitness enthusiasts, gym members following fitness programs
-   **User Personas**:
    -   **Trainer Persona**:
        -   Age: 25-45
        -   Tech-savvy, manages 10-50 clients
        -   Needs efficient client management tools
    -   **Client Persona**:
        -   Age: 18-60
        -   Mobile-first user
        -   Values simplicity and ease of logging

## 2. FUNCTIONAL REQUIREMENTS

### 2.1 Core Features

#### Feature 1: OTP Authentication

**As a** user
**I want** to authenticate via mobile OTP
**So that** I don't need to remember passwords

**Priority**: High
**Effort**: 8 story points

#### Feature 2: Client Profile Creation

**As a** trainer
**I want** to create client profiles
**So that** I can manage client information

**Priority**: High
**Effort**: 5 story points

#### Feature 3: Workout Gallery

**As a** trainer
**I want** to create and manage workouts
**So that** I can build workout plans

**Priority**: High
**Effort**: 8 story points

### 2.2 User Workflows

#### 2.2.1 Trainer Workflow: Client Onboarding

1.  Trainer logs in via OTP
2.  Trainer navigates to "Add Client"
3.  Trainer fills client form (name, phone, height, weight, goal)
4.  System creates client profile and generates invite code
5.  System sends invite SMS to client phone number

#### 2.2.2 Trainer Workflow: Assign Training Plan

1.  Trainer selects client from client list
2.  Trainer navigates to "Workouts" tab
3.  Trainer clicks "Assign Plan"
4.  Trainer builds weekly plan: Select day → Add workout → Configure sets/reps/weight
5.  Trainer sets start/end dates and adds notes
6.  Trainer saves plan

### 2.3 Business Rules

#### Business Rule 1: OTP Rules

-   OTP valid for 10 minutes
-   Maximum 3 OTP attempts per phone number per hour
-   Account temporarily blocked after 3 failed attempts (15-minute lockout)

#### Business Rule 2: Client Management Rules

-   Phone number must be unique per trainer
-   Same phone number can exist for different trainers (different clients)
-   Invite code valid for 30 days

## 3. USER STORIES

### 3.1 Epic Stories

#### Epic 1: User Authentication & Onboarding

**As a** trainer or client
**I want** to authenticate using my mobile number and OTP
**So that** I can securely access the platform without managing passwords

#### Epic 2: Client Management

**As a** trainer
**I want** to manage my clients' profiles and information
**So that** I can track their progress and provide personalized guidance

#### Epic 3: Workout Plan Management

**As a** trainer
**I want** to create and assign workout plans to clients
**So that** my clients can follow structured training programs

## 4. TECHNICAL SPECIFICATIONS

### 4.1 System Architecture

The system uses a microservices architecture with:
- Frontend: React PWA
- Backend: Node.js with NestJS
- Database: MongoDB Atlas
- Storage: AWS S3 for media files

### 4.2 API Specifications

#### Endpoint 1: POST /api/auth/request-otp

Request OTP for mobile number authentication.

#### Endpoint 2: POST /api/auth/verify-otp

Verify OTP and create JWT session.

#### Endpoint 3: POST /api/trainers/:id/clients

Create new client profile.

## 5. DATA REQUIREMENTS

### 5.1 Data Entities

#### Data Entity 1: User

- id, role, name, mobile_number, last_login, profile_photo, created_at, updated_at

#### Data Entity 2: ClientProfile

- user_id, trainer_id, dob, gender, height_cm, initial_weight_kg, goals, notes, created_at, updated_at

### 5.2 Data Relationships

- User → TrainerProfile (1:1)
- User → ClientProfile (1:1)
- Trainer → ClientProfile (1:many)

## 6. INTEGRATION REQUIREMENTS

### Integration 1: SMS Provider (Twilio)

Integration with Twilio for OTP delivery via SMS.

### Integration 2: AWS S3

Integration with AWS S3 for media file storage and CDN delivery.

## 7. REFERENCES

1. React Documentation: https://react.dev
2. NestJS Documentation: https://docs.nestjs.com
3. MongoDB Atlas: https://www.mongodb.com/cloud/atlas


