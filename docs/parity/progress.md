
## WebSocket broadcast parity

- Read the reference communication consumer and the current WebSocket bridge.
- Added singleton `WebSocketGroupRegistry` with concurrent user and room socket dictionaries plus Add/Remove/GetOthers/GetUsers operations.
- Updated `/ws/chat/{room_id}/` to register sockets in room groups, broadcast chat messages, typing events, and delete events only to other room sockets, and unregister on close.
- Updated `/ws/notifications/` to register user sockets and unregister on close.
- Updated notification publishing to push `send_notification` frames to connected user sockets through the registry.
- Preserved the existing SignalR hubs and WebSocket middleware ordering.
- Build verification succeeded with 0 errors and 9 existing warnings.
- A two-connection live WebSocket test could not be run because no running API server or valid test JWT was available in the environment; therefore no exact received-frame transcript is available to paste.

## Runtime endpoint verification report

No application code was modified. The backend was started with `dotnet run` from `server/WebOfWork.Api` and listened on `https://localhost:5001` (HTTP port 5000 redirected to HTTPS).

| Endpoint | HTTP status | Top-level JSON keys | MATCH or MISMATCH |
|---|---:|---|---|
| POST /api/auth/send-otp/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| POST /api/auth/verify-otp/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| POST /api/auth/login/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| GET /api/auth/me/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| POST /api/auth/refresh/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| GET /api/categories/ | 200 | count,next,previous,results | MATCH |
| GET /api/categories/?no_pagination=true | 200 | array | MISMATCH |
| GET /api/categories/skills/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| GET /api/common/platform-percentage/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| GET /api/common/public/faq/ | 500 | title,status,detail,instance,correlationId,traceId | MISMATCH |
| GET /api/admin/dashboard-stats/ | not run | unavailable without an admin token | NOT OBSERVED |
| GET /api/admin/pending-payouts/ | not run | unavailable without an admin token | NOT OBSERVED |
| GET /api/admin/settings/global/ | not run | unavailable without an admin token | NOT OBSERVED |

### Raw responses

POST /api/auth/send-otp/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.AuthController.SendOtp (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.AuthController.SendOtp (WebOfWork.Api)","instance":"/api/auth/send-otp/","correlationId":"5cff27a68b9b473cb90170b02bb52e1c","traceId":"0HNOQPSS1ML7I:00000001"}
```

POST /api/auth/verify-otp/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.AuthController.VerifyOtp (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.AuthController.VerifyOtp (WebOfWork.Api)","instance":"/api/auth/verify-otp/","correlationId":"30e21b0c3faa416f8cee25ab32588df1","traceId":"0HNOQPSS1ML7J:00000001"}
```

POST /api/auth/login/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.AuthController.Login (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.AuthController.Login (WebOfWork.Api)","instance":"/api/auth/login/","correlationId":"c41a448e938e400e84f8603146ac71a2","traceId":"0HNOQPSS1ML7K:00000001"}
```

GET /api/auth/me/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.AuthController.Me (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.AuthController.Me (WebOfWork.Api)","instance":"/api/auth/me/","correlationId":"108c36f4472640ebbed8e2129f650b9c","traceId":"0HNOQPSS1ML7L:00000001"}
```

POST /api/auth/refresh/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.AuthController.Refresh (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.AuthController.Refresh (WebOfWork.Api)","instance":"/api/auth/refresh/","correlationId":"9b49f9417885455aac82bd4d5e1c25af","traceId":"0HNOQPSS1ML7N:00000001"}
```

GET /api/categories/
```text
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
{"count":5,"next":null,"previous":null,"results":[{"id":1,"name":"Design"},{"id":2,"name":"Development"},{"id":3,"name":"Marketing"},{"id":4,"name":"Writing"},{"id":5,"name":"Business"}]}
```

GET /api/categories/?no_pagination=true
```text
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
[{"id":1,"name":"Design"},{"id":2,"name":"Development"},{"id":3,"name":"Marketing"},{"id":4,"name":"Writing"},{"id":5,"name":"Business"}]
```

GET /api/categories/skills/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.CategoriesController.Skills (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.CategoriesController.Skills (WebOfWork.Api)","instance":"/api/categories/skills/","correlationId":"36b850cefced410680d7a4d182e0404f","traceId":"0HNOQPSS1ML7P:00000001"}
```

GET /api/common/platform-percentage/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.ContentController.GetPlatformPercentage (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.CommonController.Percentage (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.CommonController.Percentage (WebOfWork.Api)","instance":"/api/common/platform-percentage/","correlationId":"85d957cfc3f749afbe6f8044e9547748","traceId":"0HNOQPSS1ML7Q:00000001"}
```

GET /api/common/public/faq/
```text
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json
{"title":"Internal Server Error","status":500,"detail":"The request matched multiple endpoints. Matches: \r\n\r\nWebOfWork.Api.Controllers.CommonController.Faq (WebOfWork.Api)\r\nWebOfWork.Api.Controllers.CommonController.Faq (WebOfWork.Api)","instance":"/api/common/public/faq/","correlationId":"0f9eaca3d6e5438e9ff6ad3baa3f0872","traceId":"0HNOQPSS1ML7R:00000001"}
```

Admin endpoints were not observed because no admin token was provided.

## Route attribute dedupe

Deduplicated every duplicate `[HttpX("path")]` + `[HttpX("path/")]` pair across the controllers. The trailing-slash attribute was removed; the single attribute serves both `/path` and `/path/`. This removed the `The request matched multiple endpoints` 500 errors.

- `AuthController.cs` — 13 duplicate pairs removed
- `CategoriesController.cs` — 9
- `CommonController.cs` — 5
- `ProfilesController.cs` — 12
- `ProjectsController.cs` — 7
- `ProposalsController.cs` — 1
- `AdminParityController.cs` — 12
- `CommunicationController.cs` — 8
- `ContractsController.cs`, `PaymentsController.cs`, `PayoutsController.cs` — 2, 1, 1
- `AdminUsersController.cs` / `AdminDashboardController.cs` — removed redundant `~/api/...` aliases that duplicated the `[HttpGet]`/relative attributes
- `ContentController.cs` — removed the stale `~/api/common/platform-percentage` action that collided with `CommonController`

Build: `dotnet build server/WebOfWork.Api` succeeded with 0 errors.

### Live re-verification (after fix)

| Endpoint | HTTP status | Top-level JSON keys | MATCH or MISMATCH |
|---|---:|---|---|
| POST /api/auth/send-otp/ | 200 | message | MATCH |
| POST /api/auth/verify-otp/ | 400 | error | MATCH (Invalid OTP) |
| POST /api/auth/login/ | 401 | error | MATCH (Invalid credentials) |
| GET /api/auth/me/ | 401 | (empty, 401) | MATCH (auth required) |
| POST /api/auth/refresh/ | 401 | error | MATCH (No refresh token) |
| GET /api/categories/ | 200 | count,next,previous,results | MATCH |
| GET /api/categories/skills/ | 200 | count,next,previous,results | MATCH |
| GET /api/common/platform-percentage/ | 200 | commission_percentage | MATCH |
| GET /api/common/public/faq/ | 200 | count,next,previous,results | MATCH |

Raw highlights:

```text
POST /api/auth/send-otp/  -> 200 {"message":"OTP sent to your email. Please verify."}
POST /api/auth/verify-otp/ -> 400 {"error":"Invalid OTP"}
POST /api/auth/login/     -> 401 {"error":"Invalid credentials"}
POST /api/auth/refresh/   -> 401 {"error":"No refresh token"}
GET  /api/categories/     -> 200 {"count":5,"next":null,"previous":null,"results":[...]}
GET  /api/categories/skills/ -> 200 {"count":1,"next":null,"previous":null,"results":[...]}
GET  /api/common/platform-percentage/ -> 200 {"commission_percentage":"12.00"}
GET  /api/common/public/faq/ -> 200 {"count":3,"next":null,"previous":null,"results":[...]}
```

## Cross-controller route collision fixes

`AmbiguousMatchException` ("The request matched multiple endpoints") also fires for two *different* controllers registering the same URL, not just duplicate attributes in one controller. Four were found and resolved. In each case the duplicate was removed, never both.

| URL | Was declared in | Kept in | Why |
|---|---|---|---|
| `GET /api/admin/settings/global` | `AdminParityController` + `PlatformSettingsController` | **PlatformSettingsController** | runs `UpdateGlobalSettingsRequestValidator`; returns `ValidationProblemDetails`, which matches the reference's DRF serializer error shape |
| `PATCH /api/admin/settings/global` | same | **PlatformSettingsController** | same reason |
| `GET /api/admin/pending-payouts` | `AdminParityController` + `AdminDashboardController` | **AdminParityController** | returns the reference item shape, with `payment_id = "PAY-{Payment.Id}"` (the contract-id bug is fixed there) |
| `GET /api/profile/freelancer/me` | `FreelancersController` + `ProfilesController` | **ProfilesController** | returns the reference *small* serializer; the richer management payload stays at `/api/profile/freelancer/profile` |

Each removal is marked in-place with a `// NOTE:` comment explaining the conflict and where the surviving implementation lives. `AdminAnalyticsService.GetPendingPayoutsAsync` is untouched and still backs `/api/payouts/pending`.

### Live verification (no ambiguous-match 500s remain)

| URL | HTTP status | Meaning |
|---|---:|---|
| `/api/admin/settings/global/` | 401 | auth required, single endpoint matched |
| `/api/admin/pending-payouts/` | 401 | auth required, single endpoint matched |
| `/api/profile/freelancer/me/` | 401 | auth required, single endpoint matched |
| `/api/common/platform-percentage/` | 200 | `{"commission_percentage":"12.00"}` |
| `/api/categories/skills/` | 200 | paginated envelope |
| `/api/contracts/1/` | 401 | single endpoint matched |
| `/api/deliverables/contract/1/` | 401 | single endpoint matched |
| `/api/revisions/contract/1/` | 401 | single endpoint matched |
| `/api/briefs/` | 405 | method not allowed, no GET defined |
| `/api/proposals/` | 405 | method not allowed, no GET defined |
| `/api/freelancers/` | 200 | OK |

401 = endpoint resolved, authorization required. 405 = resolved, verb not allowed. No `500` and no `matched multiple endpoints` responses.

Build: `dotnet build server/WebOfWork.Api` succeeded with 0 errors.
