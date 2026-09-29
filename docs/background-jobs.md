# Background Jobs

This document captures the Phase 17 job architecture for WebOfWork and the runtime expectations described in the project plan.

## Phase 17 scope

Phase 17 implements the migrated background job layer that replaces the legacy Celery-style task pattern with Hangfire using PostgreSQL storage.

The stable implementation lives in:

- [server/WebOfWork.Api/Jobs/HangfireConfiguration.cs](../server/WebOfWork.Api/Jobs/HangfireConfiguration.cs)
- [server/WebOfWork.Api/Jobs/SendOtpEmailJob.cs](../server/WebOfWork.Api/Jobs/SendOtpEmailJob.cs)
- [server/WebOfWork.Api/Jobs/SendPasswordResetEmailJob.cs](../server/WebOfWork.Api/Jobs/SendPasswordResetEmailJob.cs)
- [server/WebOfWork.Api/Jobs/SendPhoneOtpJob.cs](../server/WebOfWork.Api/Jobs/SendPhoneOtpJob.cs)
- [server/WebOfWork.Api/Jobs/GenerateContractDocumentJob.cs](../server/WebOfWork.Api/Jobs/GenerateContractDocumentJob.cs)
- [server/WebOfWork.Api/Jobs/SendSupportReplyJob.cs](../server/WebOfWork.Api/Jobs/SendSupportReplyJob.cs)
- [server/WebOfWork.Api/Jobs/PublishNotificationJob.cs](../server/WebOfWork.Api/Jobs/PublishNotificationJob.cs)
- [server/WebOfWork.Api/Jobs/CleanupExpiredOtpsJob.cs](../server/WebOfWork.Api/Jobs/CleanupExpiredOtpsJob.cs)

## Architecture

### Hangfire storage

The application configures Hangfire with PostgreSQL and runs a dedicated worker pool. This is defined in [server/WebOfWork.Api/Jobs/HangfireConfiguration.cs](../server/WebOfWork.Api/Jobs/HangfireConfiguration.cs).

Key characteristics:

- job storage: PostgreSQL
- dashboard: `/hangfire`
- queues: `critical`, `default`, `low`
- recurring jobs: scheduled cleanup and aggregate maintenance tasks

### Runtime pattern

The normal pattern is:

1. an API/service method validates business rules
2. it stores the ephemeral OTP, notification, or document metadata
3. it enqueues a Hangfire job
4. Hangfire executes the job asynchronously
5. the job performs the external side effect such as email, SMS, PDF generation, or SignalR publish

This approach keeps the request path fast and preserves the original Celery task separation while staying in the .NET/Hangfire ecosystem.

## Job inventory

| Job                           | Responsibility                 | Trigger path               | External effect         |
| ----------------------------- | ------------------------------ | -------------------------- | ----------------------- |
| `SendOtpEmailJob`             | send email OTP                 | auth service               | SMTP via `IEmailSender` |
| `SendPasswordResetEmailJob`   | send password reset flow email | password reset service     | SMTP via `IEmailSender` |
| `SendPhoneOtpJob`             | send phone OTP                 | phone verification service | SMS via Android gateway |
| `GenerateContractDocumentJob` | generate contract PDF          | contract workflow          | document generation     |
| `SendSupportReplyJob`         | send support reply email       | admin support reply flow   | SMTP via `IEmailSender` |
| `PublishNotificationJob`      | publish notification           | notification domain events | SignalR + optional SMS  |
| `CleanupExpiredOtpsJob`       | remove stale OTPs              | recurring schedule         | Redis/OTP cleanup       |

## Email and SMS job behavior

### OTP email job

`SendOtpEmailJob` delivers the generated OTP through the configured email sender. The job is intended to be fired after the OTP has been created and stored in Redis. The real sending path is abstracted behind `IEmailSender`, which allows the application to swap providers without altering the job contract.

### Phone OTP job

`SendPhoneOtpJob` dispatches the OTP to the normalized phone number. Sri Lankan numbers are normalized to E.164 form, for example:

- `0779091086` -> `+94779091086`

This is the required validation target for the Phase 17 smoke test and the corresponding local verification script.

### Retry policy

The SMS gateway integration includes retry behavior for temporary failures. The retry policy is implemented in the SMS sender layer and is expected to retry transient responses such as HTTP 5xx and network timeouts instead of failing the whole job immediately.

The relevant implementation and tests are:

- [server/WebOfWork.Api/Integrations/Sms/AndroidSmsGatewaySmsSender.cs](../server/WebOfWork.Api/Integrations/Sms/AndroidSmsGatewaySmsSender.cs)
- [Tests/WebOfWork.Api.Tests/Sms/AndroidSmsGatewaySmsSenderTests.cs](../Tests/WebOfWork.Api.Tests/Sms/AndroidSmsGatewaySmsSenderTests.cs)

## Verification checklist

The following checks are the operational proof points for the phase:

1. Trigger OTP email flow and confirm job execution in Hangfire.
2. Trigger phone OTP flow with `0779091086` and confirm the outgoing number is `+94779091086`.
3. Confirm `SendPhoneOtpJob` and `SendOtpEmailJob` appear in the Hangfire succeeded list.
4. Trigger a temporary SMS failure (HTTP 500/503/timeout) and confirm retry behavior occurs.
5. Trigger a support reply and confirm `SendSupportReplyJob` succeeds.
6. Trigger a document-generation path and confirm `GenerateContractDocumentJob` succeeds.
7. Trigger a notification-producing action and confirm `PublishNotificationJob` succeeds.
8. Confirm the recurring cleanup job executes and OTP maintenance remains healthy.

## Manual verification script

The local validation script for this phase is:

- [scratch/run_local_test_phase17.ps1](../scratch/run_local_test_phase17.ps1)

This script verifies:

- API reachability
- OTP email send route
- phone OTP send route
- Sri Lankan phone normalization
- Hangfire dashboard visibility
- the manual checklists for support, contract, notification, and retry validation

## Notes and implementation status

The project’s Phase 17 behavior is considered complete when the following are all true:

- Hangfire starts and is storing jobs in PostgreSQL
- background job types execute successfully in the dashboard
- OTP flows send outbound email and SMS without silently failing
- retry logic covers temporary SMS gateway faults
- notification and support jobs execute through the same job infrastructure

This matches the migration plan from Django/Celery-style tasks to .NET/Hangfire and is the operational replacement for the legacy task layer referenced in the private plan.
