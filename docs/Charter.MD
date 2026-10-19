# Project Charter: GymTracker

## 1. Project Purpose and Objectives
GymTracker is an Artificial Intelligence assisted fitness planning and tracking application that helps people organize
training, record easily what they actually completed in natural language, understand progress with visual artefacts, and stay connected with training partners. Also it enables to user make more structured workout to reach their goals. Its core components are: setting specific goals, choosing or creating personalized programs, scheduling training day and times,
actively training and recording workout, reviewing the previous workout to optimise  future   workouts, at the end adjusting balance of tranining. The Artificial Intelligence assistance aims to reduce the effort of entering workout records and organizing information, while users retain control over profile, program, and workout saved to their account. Also the visual design of the application offers people to use more personalized and aesthetic experience.

The application addresses four related problems. First is fragmented training records entries, it is time consuming that entering the program and workout entirely by hand. Secondly the progress information without clear definitions can be decresase the progress and consume time and energy of the person. Third one is the difficulty maintaining routines or coordinating training, with/out friends, because it is not easy to coordinate and maintain training routines in a highly busy life. At the end people can over/underestimate their specific muscle groups. The structured exercise data and transparent calculations are the factual foundation of estimating the body muscle progress/ power rate. Also  Artificial Intelligence explains or proposes information, it provides user to previous Workout schema respect to  user input and at the end user shall approve the Workout. So Artificial Intelligence is not the authoritative source of historical results.

The intended outcome is an integrated academic pilot with demonstrable end-to-end behavior, documented
architecture, and evidence of testing.

## 2. Stakeholders & Roles
* **Product Owner:** 
    * Selim Efe Aytemur / aytemur23@itu.edu.tr
* **Software Engineering Team:** 
    * Ahmet Abalı : Project Manager, Frontend Developer / abali24@itu.edu.tr
    * Can Sei Keçeci : Backend Developer, AI Engineer, Documenter / kececi24@itu.edu.tr
    * Hamza Çevikbaş : Full Stack Developer, DevOps Engineer cevikbash24@itu.edu.tr
    * Selim Efe Aytemur : Frontend Developer, Database Engineer / aytemur23@itu.edu.tr
* **GH Repo/Project URL:** (https://github.com/kececi24/gymtracker)
* **Customer:** 
    * Res. Ass. Kemal Devrim Kafadar / kafadar19@itu.edu.tr

* **Users:** 
    * Fitness Enthusiasts
    * Professional Athletes
    * Elite Trainers
    * Sporters from age 16 to 70+


## 3. Project Scope
### Project domain
The product is an Artificial Intelligence assisted Fitness/Workout planning and tracking app for individuals. Artificial Intelligence used to make training plan fastly and recording workout easily in natural language prompts, the visual progress bars and charters improve user's progress not only  mentally  also improve pyhsically. The project estimated users are fitness enthusiasts, professional athlete, elite trainers, beginners. Main release platform is mobile, but we may also  include Web Application similar to Mobile Application. Android platform is the prioritized platform. Due to easy to prototype chance. Our in/out scope and technologies will be used, explained in below.


### In-Scope 
* Welcome Screens
* User Authentication via Email/Google Account
* Onboarding 
* Goal Selection Manually/ Goal Suggestions with AI
* Exercise Catalogue for different muscle groups
* Predefined Workout Schemas
* Workout Creation manually/ Creation in use of AI assistance
* Calendar and weekly/monthly planning
* Manually Live Workout Logging
* Natural Language After Workout Logging
* Manually After Workout Logging
* Reviewing History and Measured Progress
* Progress dashboard
* Personalized insights
* Daily Motivated Quotes
* Reminders and notifications before scheduled Workout 
* User Badges
* Friends connections
* Joint workouts
* Moderation
* Workout Sharing
* Personal Records
* Friend group chat


### Out-of-Scope 
* Medical diagnosi
* Injury assessment, rehabilitation, emergency monitoring, or clinical claims.
* Gym billing, membership management, trainer payments, or e-commerce.
* Camera-based form analysis, automatic repetition counting, voice transcription, or biometric identity verification.
* Automatic supplement, drug, or diet prescription; meal databases, barcode scanning, macronutrient diaries, or AI meal recognition.
* Direct wearable, Bluetooth, Apple Health, Health Connect, or live heart-rate integration in the initial pilot.
* Autonomous AI writes, unreviewed training-plan changes, or AI access to friends’ private records.
* Cardio distance/GPS tracking, competition administration, advanced periodization automation, and live collaborative editing of one shared workout log.
* Custom model training, a proprietary training dataset, or a promise of production-scale availability.

## 4. Technology Stack
### Backend Stack
* C# — Backend programming language
* .NET 10 LTS — Backend runtime and application framework
* ASP.NET Core — APIs, authentication, business logic, and AI orchestration
* Entity Framework Core — Database access and migrations
* REST APIs — Communication between frontend and backend
* OpenAPI — API documentation and contract definition

### Database & Messaging Stack
* MySQL 8.4 — Persistent application data storage
* Pomelo.EntityFrameworkCore.MySql — MySQL connectivity for .NET
* Database Migrations — Version-controlled database schema changes
* Background Jobs — Asynchronous tasks and scheduled operations

### Frontend Stack
* Flutter — Cross-platform application development
* Dart — Flutter programming language
* Riverpod — State management and dependency injection
* Dio — HTTP requests and API communication

### Mobile Stack
* Flutter — Shared Android and iOS application
* Android — Android platform support
* iOS — Apple mobile platform support
* Drift / SQLite — Local storage, offline logging, and synchronization queues
* Firebase Cloud Messaging — Push notifications

### Authentication & Firebase Stack
* Firebase Authentication — User registration, login, and identity management
* Google Sign-In — Google account authentication
* Firebase Cloud Messaging — Push notifications and reminders
* Firebase Admin SDK — Server-side Firebase authentication and messaging operations
* Firebase App Check — Help protect backend resources from unauthorized clients

### AI Integration Stack
* LLM API Integration — AI-powered features and responses
* Structured AI Responses — Consistent, machine-readable AI output
* AI-Based Workout Plan Generation — Personalized workout recommendations

### Testing Stack
* xUnit — Backend unit testing
* Flutter Unit Tests — Testing business logic
* Flutter Widget Tests — Testing UI components
* PostgreSQL Integration Tests — Verifying database operations
* API Integration Tests — Testing backend endpoints

### DevOps & Infrastructure Stack
* Docker — Containerizing applications and services
* Docker Compose — Running local development environments
* Kubernetes — Container orchestration and scaling
* Helm — Managing Kubernetes deployments
* GitHub Actions — Automated testing and CI/CD
* Trivy — Container and dependency vulnerability scanning


### AI Stack
* OpenAI's ChatGPT 5.5 Sol/Luna/Tera and ChatGpt 6.0 Astra models in Low/Medium/High efforts.
* Anthropic's Claude Opus 5.5/Sonnet 5.5/Haiku models in Low/Medium/High efforts.
* xAI's Grok 4.6/4.7 models in Low/Medium/High efforts.
* Google's Gemini 2.5/3/3.1/3.8 Flash/ 4 models
* Google's BigQuery
* Cursor


## 5. Project Milestones
| Deliverable                       | Deadline          |
|---                                |---                |
| Project Charter                   | 05.10.2026 23:59  |
| Project Plan                      | 19.10.2026 23:59  |
| Requirement Specification Report  | 02.11.2026 23:59  |
| Requirement Analysis Report       | 23.11.2026 23:59  |
| Software Design Report            | 07.12.2026 23:59  |
| Software Test Report              | 21.12.2026 23:59  |
| Codebase                          | 25.12.2026 23:59  |
| Demo                              | 26.12.2026 23:59  |
| Presentation                      | 28.12.2026 23:59  |
	
	
	
	
	
	
	
	
