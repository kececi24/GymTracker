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


## 3. Project Scope
### Project domain
GymTracker is an AI-assisted fitness planning and workout tracking application for individuals. It helps users plan training, record completed exercises, and review progress through workout history and charts. The platform is planned to serve a wide range of users, from fitness enthusiasts and elite trainers to professional athletes and everyday sports participants aged 16 to 70+. The project will deliver a working Android mobile application connected to a backend API and database. Web and iOS applications are possible future extensions and are not required for the initial delivery.


### In-Scope 
The application will support the following workflows:

* **Account setup and goals:** Users can sign up and sign in with email or Google, complete onboarding, edit their profile, and set fitness goals manually or review AI suggestions.
* **Workout planning:** Users can browse exercises by muscle group, choose a predefined program, or create and edit their own program. AI can propose a program for the user to review and confirm before saving.
* **Scheduling and reminders:** Users can schedule, reschedule, and cancel workouts in weekly and monthly calendar views and receive reminders before scheduled sessions.
* **Workout recording:** Users can enter exercises, sets, repetitions, and weights during or after a workout. They can also type a description such as "I did three sets of ten squats at 40 kg" after training; AI converts it into an editable workout draft that is saved only after confirmation. Local drafts and queued synchronization to allow users record workouts without a connection.
* **History and progress:** Users can review, edit, and delete their workout records and view charts of training frequency, total weight lifted across sets, muscle-group workload, and personal records. AI can explain patterns in the saved records to help users review future plans.
* **Friends and private sharing:** Users can send and accept friend requests, remove connections, and share completed-workout summaries with selected friends. They can revoke access to a shared summary.

The delivery also includes the backend API, persistent database, application build, deployment instructions, and test evidence needed to run and demonstrate these workflows.


### Out-of-Scope 
The following extensions are not required for the initial delivery. They may be considered after the core workflows are complete and the team reviews the remaining time:

* **Additional clients:** A web application and an iOS version of the mobile application.
* **Motivation features:** Achievement badges and daily motivational quotes.
* **Training together:** Friend-group text chat and joint workout invitations, with each participant keeping a separate workout record.
* **Public program sharing and moderation:** Publishing programs for other users to browse and copy, with reporting and moderation tools for public content.
* **Additional tracking:** Basic nutrition diaries, GPS-based cardio tracking, and importing workout data from wearables or health platforms.

## 4. Technology Stack
### Backend Stack
* C# — Backend programming language
* .NET 10 LTS — Backend runtime and application framework
* ASP.NET Core — APIs, authentication, business logic, and AI orchestration
* Entity Framework Core — Database access and migrations
* REST APIs — Communication between frontend and backend
* OpenAPI — API documentation and contract definition
* PostgreSQL — Persistent application data storage
* Database Migrations — Version-controlled database schema changes
* Background Jobs — Asynchronous tasks and scheduled operations
* Firebase Admin SDK — Server-side Firebase token verification and messaging operations
* xUnit — Backend unit testing
* MySQL Integration Tests — Verifying database operations against the selected database
* API Integration Tests — Testing backend endpoints
* Docker and Docker Compose — Containerizing services and running local development environments
* Kubernetes and Helm — Optional deployment and container orchestration after the core delivery
* GitHub Actions — Automated testing and CI/CD
* Trivy — Container and dependency vulnerability scanning
* Google's BigQuery — Optional analytics tooling

### Frontend Stack
* Flutter — Android application development, with potential reuse for future web and iOS clients
* Dart — Flutter programming language
* Riverpod — State management and dependency injection
* Dio — HTTP requests and API communication
* Android — Initial mobile delivery platform
* Drift / SQLite — Local storage, offline logging, and synchronization queues
* Firebase Cloud Messaging — Push notifications and reminders
* Firebase Authentication — User registration, login, and identity management
* Google Sign-In — Google account authentication
* Firebase App Check — Help protect backend resources from unauthorized clients
* Flutter Unit Tests — Testing client business logic
* Flutter Widget Tests — Testing UI components

### AI Stack
* LLM API Integration — AI-powered features and responses
* Structured AI Responses — Consistent, machine-readable AI output
* AI-Based Workout Plan Generation — Personalized workout drafts requiring user confirmation
* Natural-Language Workout Conversion — Turning a user's workout description into an editable record

The following AI tools are candidates for integration or development assistance; the project does not require integrating every listed model:

* OpenAI's ChatGPT 5.5 Sol/Luna/Tera and ChatGpt 6.0 Astra models in Low/Medium/High efforts.
* Anthropic's Claude Opus 5.5/Sonnet 5.5/Haiku models in Low/Medium/High efforts.
* xAI's Grok 4.6/4.7 models in Low/Medium/High efforts.
* Google's Gemini 2.5/3/3.1/3.8 Flash/ 4 models
* Cursor


## 5. Project Milestones
| Deliverable                       | Deadline          |
|---                                |---                |
| Project Charter                   | 19.10.2026 23:59  |
| Project Plan                      | 25.10.2026 23:59  |
| Requirement Specification Report  | 02.11.2026 23:59  |
| Requirement Analysis Report       | 23.11.2026 23:59  |
| Software Design Report            | 07.12.2026 23:59  |
| Software Test Report              | 21.12.2026 23:59  |
| Codebase                          | 25.12.2026 23:59  |
| Demo                              | 26.12.2026 23:59  |
| Presentation                      | 28.12.2026 23:59  |
	
	
	
	
	
	
	
	
