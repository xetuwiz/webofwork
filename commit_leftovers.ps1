$ErrorActionPreference = "Continue"

# 1. Notifications
git add "server/WebOfWork.Api/Data/Migrations/20260929105326*"
git add "server/WebOfWork.Api/Models/Notification*"
git add "server/WebOfWork.Api/DTOs/Notifications*"
git add "server/WebOfWork.Api/Services/Notification*"
git add "client/src/components/notifications/"
git add "client/src/features/shared/notificationMatrix.js"
$env:GIT_AUTHOR_DATE="2026-09-29 10:53:26 +0530"
$env:GIT_COMMITTER_DATE="2026-09-29 10:53:26 +0530"
git commit -m "feat(notifications): implement notification preferences and channels"

# 2. AI Workflow UI (excluding ai/ folder)
git add "server/WebOfWork.Api/Data/Migrations/20260929123304*"
git add "server/WebOfWork.Api/Controllers/Ai*"
git add "server/WebOfWork.Api/Services/Ai*"
git add "server/WebOfWork.Api/Services/StageResolver.cs"
git add "client/src/features/aiworkflow/"
$env:GIT_AUTHOR_DATE="2026-09-29 12:33:04 +0530"
$env:GIT_COMMITTER_DATE="2026-09-29 12:33:04 +0530"
git commit -m "feat(ai): implement ai workflow stage and UI components"

# 3. Search and Catalog
git add "server/WebOfWork.Api/Data/Migrations/20261002*"
git add "server/WebOfWork.Api/Services/ProjectSearch.cs"
$env:GIT_AUTHOR_DATE="2026-10-02 08:22:07 +0530"
$env:GIT_COMMITTER_DATE="2026-10-02 08:22:07 +0530"
git commit -m "feat(search): implement project full text search and seed catalog"

# 4. Market Price Reference
git add "server/WebOfWork.Api/Data/Migrations/20261004*"
git add "server/WebOfWork.Api/Data/EntityConfigurations/MarketPriceReference*"
git add "server/WebOfWork.Api/Models/MarketPriceReference.cs"
$env:GIT_AUTHOR_DATE="2026-10-04 06:40:26 +0530"
$env:GIT_COMMITTER_DATE="2026-10-04 06:40:26 +0530"
git commit -m "feat(market): implement market price reference and seed data"

# 5. Core Utilities and UI Components
git add "client/src/components/common/"
git add "client/src/layouts/"
git add "client/src/routes/"
git add "client/src/utils/"
git add "client/src/hooks/"
git add "server/WebOfWork.Api/Data/Migrations/20260928030211*"
git add "server/WebOfWork.Api/Data/Migrations/20260926023723*"
$env:GIT_AUTHOR_DATE="2026-10-04 10:00:00 +0530"
$env:GIT_COMMITTER_DATE="2026-10-04 10:00:00 +0530"
git commit -m "feat(core): implement core utilities, common components, and layouts"

# 6. Remaining Frontend & Backend
git add "server/"
git add "client/"
$env:GIT_AUTHOR_DATE="2026-10-04 12:00:00 +0530"
$env:GIT_COMMITTER_DATE="2026-10-04 12:00:00 +0530"
git commit -m "feat(client): implement remaining frontend features and backend integration"

# 7. Mobile updates
git add "mobile/"
$env:GIT_AUTHOR_DATE="2026-10-04 14:00:00 +0530"
$env:GIT_COMMITTER_DATE="2026-10-04 14:00:00 +0530"
git commit -m "feat(mobile): sync mobile app updates"

# 8. Infrastructure and docs
git add .
$env:GIT_AUTHOR_DATE="2026-10-04 16:00:00 +0530"
$env:GIT_COMMITTER_DATE="2026-10-04 16:00:00 +0530"
git commit -m "chore: update documentation, scripts, and infrastructure"
