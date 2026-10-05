# Authentication API Documentation

## Overview
WebOfWork uses JWT (JSON Web Tokens) for authentication. Roles include Client, Freelancer, and Admin.

## Endpoints

### 1. `POST /api/auth/send-otp`
Sends a 6-digit OTP to the user's email for registration.
- **Body**: `{ "email": "user@example.com" }`

### 2. `POST /api/auth/verify-otp`
Verifies the OTP and creates the user account.
- **Body**: `{ "email": "user@example.com", "otp": "123456", "password": "Pass123!", "fullName": "John Doe", "role": 1 }`
- **Response**: JWT Access Token and sets `HttpOnly` Refresh Token cookie.

### 3. `POST /api/auth/login`
Standard email/password login.
- **Body**: `{ "email": "user@example.com", "password": "Pass123!" }`

### 4. `POST /api/auth/refresh`
Refreshes the access token using the `HttpOnly` refresh token cookie.

### 5. `POST /api/auth/logout`
Clears the `HttpOnly` refresh token cookie.

### 6. `GET /api/auth/me`
Retrieves the current authenticated user's profile.

### 7. `PATCH /api/auth/me`
Updates user profile. Changing email triggers re-verification.

### 8. `POST /api/auth/forgot-password` / `reset-password` / `change-password`
Standard password lifecycle endpoints.
