
# XBCAD MVC Application
## Overview

This is a web application built with ASP.NET Core MVC that integrates Firebase for authentication and real-time database management. It supports multiple user roles, including **Admins** and **Clients**, and offers features like user authentication (Google OAuth), session management, and interaction through Firebase.

### Features

- **Firebase Authentication**: Supports Google OAuth login and token verification.
- **Role-Based Access**: Different dashboards for Admins and Clients.
- **Session Management**: Admins can manage client sessions, update payment statuses, and track earnings.
- **Real-Time Communication**: Chat features powered by Firebase Realtime Database.
- **Google Calendar Integration**: Integration with Google Calendar for session bookings.
- **Profile Management**: Users can manage their profiles, upload profile images, and modify their availability.

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Controllers Overview](#controllers-overview)
3. [ViewModels Overview](#viewmodels-overview)
4. [Firebase Integration](#firebase-integration)
5. [Setup and Installation](#setup-and-installation)
6. [Usage](#usage)
7. [License](#license)

---

## Project Structure

The project is structured as follows:

```
/Controllers
    AccountController.cs
    AdminController.cs
    ClientController.cs
/Models
    ErrorViewModel.cs
    FirebaseError.cs
/ViewModels
    AvailabilityViewModel.cs
    BookedSession.cs
    ClientPaymentSummaryViewModel.cs
    ClientSessionsViewModel.cs
    ClientViewModel.cs
    LoginViewModel.cs
    RegisterViewModel.cs
    TrainerAvailabilityViewModel.cs
/Services
    FirebaseService.cs
    GoogleCalendarService.cs
/Views
    (All your views for Admin, Client, Account, etc.)
/wwwroot
    (Static files such as JavaScript, CSS, images)
```

---

## Controllers Overview

### AccountController
- Handles user authentication through Google OAuth.
- Manages login, token verification, and redirects users based on their roles (Admin or Client).
- Interacts with Firebase to manage user data in real-time.

### AdminController
- Provides functionality for admins to manage client sessions and payments.
- Integrates with Google Calendar to manage training sessions.
- Admins can update profiles, availability, and delete accounts.
- Admin Dashboard displays data fetched from Firebase.

### ClientController
- Clients can manage their profile, book training sessions, and chat with trainers.
- Integrates Google Calendar to display training sessions.
- Similar to AdminController but tailored for clients.



---

## ViewModels Overview

### AvailabilityViewModel
- Represents user availability across different days and time slots.

### BookedSession
- Holds details of a booked session between a client and a trainer, including the session's payment status and time.

### ClientPaymentSummaryViewModel
- Displays summary information of payments made by a client for trainer services.

### ClientSessionsViewModel
- Combines client details and their session information for display purposes.

### ClientViewModel
- Contains basic client details like ID, name, and profile image URL.

### LoginViewModel
- Contains login credentials and token for Firebase authentication.

### Trainer and TrainerAvailabilityViewModel
- Trainer represents the trainer's details.
- TrainerAvailabilityViewModel holds both trainer information and their available time slots for client bookings.

---

## Firebase Integration

### Firebase Authentication

Firebase Admin SDK is used for authenticating users, managing tokens, and verifying users' identities. Google OAuth is used to log in via Google accounts, and users are redirected based on their role.

### Firebase Realtime Database

- **Session Management**: All client and admin session details are stored in Firebase. Admins can update session statuses and manage payments.
- **Profile Data**: Client and admin profile data, including names, profile pictures, and rates, are stored and fetched from Firebase.

### Google Calendar Integration

The **GoogleCalendarService** is responsible for creating, managing, and embedding calendar events. Clients can book sessions with trainers, and these bookings are synchronized with the trainer’s Google Calendar.

---

## Setup and Installation

### Prerequisites

Ensure you have the following installed:
- **Visual Studio 2022** (or later) with ASP.NET Core support.
- **Firebase Project**: You must set up a Firebase project and obtain the Google service account credentials.
- **Google OAuth Credentials**: For Google login, you'll need to configure Google OAuth in your Firebase project.

### Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/UndeadRonin99/Alleysway-website.git
   cd XBCAD
   ```

2. Open the project in Visual Studio.

3. Install the required NuGet packages:
   - FirebaseAdmin
   - Google.Apis.Auth
   - Microsoft.AspNetCore.Authentication.Google
   - Newtonsoft.Json

4. Set up **Firebase credentials**:
   - Download the `serviceAccountKey.json` from your Firebase project.
   - Place this file in the project root and configure it in your `AccountController`.

5. Configure **Google OAuth**:
   - In Firebase, add your web app and enable Google sign-in.
   - Set the `RedirectUri` in the `GoogleLogin` method in the `AccountController`.

6. Set up **Google Calendar API**:
   - Enable the Google Calendar API in the Google Cloud Console.
   - Set up OAuth credentials and integrate them into `GoogleCalendarService`.

7. Run the project:
   Press `F5` in Visual Studio or use:
   ```bash
   dotnet run
   ```

---

## Usage

### Admin Functionality
- **Login**: Admins can log in via Google OAuth and access the admin dashboard.
- **Session Management**: Admins can manage sessions, update payment statuses, and view trainer availability.
- **Profile Management**: Admins can upload profile pictures, set rates, and update availability.

### Client Functionality
- **Login**: Clients can log in via Google OAuth and access the client dashboard.
- **Book Sessions**: Clients can view trainer availability and book sessions.
- **Profile Management**: Clients can upload profile pictures and manage their session history.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more information.

---

