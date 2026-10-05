$ErrorActionPreference = "Stop"
$src = "C:\catacolite\webofwork"
$dest = "C:\catacolite\webofwork-git"

Set-Location $dest

# Ensure we are up to date and on a clean slate
git checkout main
git pull --ff-only

# Create and checkout branch
git checkout -b Profiles-ProjectManagement

$commits = @(
    @{
        msg = "feat(categories): implement category and skill models, DTOs, and configuration"
        date = "2026-09-25 17:04:12 +0530"
        paths = @(
            "server/WebOfWork.Api/Models/Category.cs",
            "server/WebOfWork.Api/Models/Skill.cs",
            "server/WebOfWork.Api/Models/FreelancerSkill.cs",
            "server/WebOfWork.Api/DTOs/Categories/CategoryResponse.cs",
            "server/WebOfWork.Api/DTOs/Categories/SkillResponse.cs",
            "server/WebOfWork.Api/DTOs/Categories/CreateCategoryRequest.cs",
            "server/WebOfWork.Api/DTOs/Categories/CreateSkillRequest.cs",
            "server/WebOfWork.Api/DTOs/Profiles/SkillDto.cs",
            "server/WebOfWork.Api/Data/EntityConfigurations/CategoryConfiguration.cs",
            "server/WebOfWork.Api/Data/EntityConfigurations/SkillConfiguration.cs"
        )
    },
    @{
        msg = "feat(categories): add category service and controller"
        date = "2026-09-25 17:36:47 +0530"
        paths = @(
            "server/WebOfWork.Api/Services/ICategoryService.cs",
            "server/WebOfWork.Api/Services/CategoryService.cs",
            "server/WebOfWork.Api/Controllers/CategoriesController.cs"
        )
    },
    @{
        msg = "feat(profiles): implement client and freelancer profile models, configurations"
        date = "2026-09-25 18:08:23 +0530"
        paths = @(
            "server/WebOfWork.Api/Models/ClientProfile.cs",
            "server/WebOfWork.Api/Models/ClientProfileCategory.cs",
            "server/WebOfWork.Api/Models/FreelancerProfile.cs",
            "server/WebOfWork.Api/Data/EntityConfigurations/ProfileConfigurations.cs"
        )
    },
    @{
        msg = "feat(profiles): add profile DTOs and validators"
        date = "2026-09-25 18:41:05 +0530"
        paths = @(
            "server/WebOfWork.Api/DTOs/Profiles/ClientProfileResponse.cs",
            "server/WebOfWork.Api/DTOs/Profiles/UpdateClientProfileRequest.cs",
            "server/WebOfWork.Api/DTOs/Profiles/FreelancerProfileResponse.cs",
            "server/WebOfWork.Api/DTOs/Profiles/PublicFreelancerProfileResponse.cs",
            "server/WebOfWork.Api/DTOs/Profiles/UpdateFreelancerProfileRequest.cs",
            "server/WebOfWork.Api/DTOs/Profiles/FreelancerOnboardingRequest.cs",
            "server/WebOfWork.Api/DTOs/Profiles/ParityProfileDtos.cs",
            "server/WebOfWork.Api/DTOs/Profiles/FreelancerListItemResponse.cs",
            "server/WebOfWork.Api/Validators/Profiles/UpdateClientProfileValidator.cs",
            "server/WebOfWork.Api/Validators/Profiles/UpdateFreelancerProfileValidator.cs"
        )
    },
    @{
        msg = "feat(profiles): implement profile services and controllers"
        date = "2026-09-25 19:12:38 +0530"
        paths = @(
            "server/WebOfWork.Api/Services/ProfileService.cs",
            "server/WebOfWork.Api/Services/FreelancerOnboardingService.cs",
            "server/WebOfWork.Api/Controllers/ProfilesController.cs",
            "server/WebOfWork.Api/Controllers/FreelancersController.cs"
        )
    },
    @{
        msg = "feat(portfolio): implement portfolio models, configuration, DTOs, validator"
        date = "2026-09-25 19:47:19 +0530"
        paths = @(
            "server/WebOfWork.Api/Models/FreelancerPortfolio.cs",
            "server/WebOfWork.Api/Models/PortfolioMedia.cs",
            "server/WebOfWork.Api/Data/EntityConfigurations/PortfolioMediaConfiguration.cs",
            "server/WebOfWork.Api/DTOs/Portfolio/PortfolioMediaResponse.cs",
            "server/WebOfWork.Api/DTOs/Portfolio/UploadPortfolioMediaRequest.cs",
            "server/WebOfWork.Api/Validators/Portfolio/UploadPortfolioMediaValidator.cs"
        )
    },
    @{
        msg = "feat(portfolio): add portfolio service"
        date = "2026-09-25 20:21:56 +0530"
        paths = @(
            "server/WebOfWork.Api/Services/IPortfolioService.cs",
            "server/WebOfWork.Api/Services/PortfolioService.cs"
        )
    },
    @{
        msg = "feat(projects): implement project models, enums, configuration"
        date = "2026-09-25 20:56:31 +0530"
        paths = @(
            "server/WebOfWork.Api/Models/ProjectModels.cs",
            "server/WebOfWork.Api/Models/Enums/ProjectEnums.cs",
            "server/WebOfWork.Api/Data/EntityConfigurations/ProjectConfigurations.cs"
        )
    },
    @{
        msg = "feat(projects): add project DTOs and validators"
        date = "2026-09-25 21:33:14 +0530"
        paths = @(
            "server/WebOfWork.Api/DTOs/Projects/ProjectDtos.cs",
            "server/WebOfWork.Api/Validators/Projects/ProjectValidators.cs"
        )
    },
    @{
        msg = "feat(projects): implement project service, search, recommendations, and controller"
        date = "2026-09-25 22:07:49 +0530"
        paths = @(
            "server/WebOfWork.Api/Services/ProjectService.cs",
            "server/WebOfWork.Api/Services/ProjectSearch.cs",
            "server/WebOfWork.Api/Services/ProjectRecommendationService.cs",
            "server/WebOfWork.Api/Controllers/ProjectsController.cs"
        )
    },
    @{
        msg = "feat(categories): implement admin category and skill management UI"
        date = "2026-09-26 15:07:22 +0530"
        paths = @(
            "client/src/features/admin/categorymanagement/"
        )
    },
    @{
        msg = "feat(profiles): implement client profile overview, edit, and interests UI"
        date = "2026-09-26 15:42:09 +0530"
        paths = @(
            "client/src/features/client/profile/"
        )
    },
    @{
        msg = "feat(profiles): implement freelancer profile, editing, and portfolio UI"
        date = "2026-09-26 16:18:35 +0530"
        paths = @(
            "client/src/features/freelancer/profile/",
            "client/src/features/client/findtalent/FreelancerProfile.jsx"
        )
    },
    @{
        msg = "feat(settings): implement client and freelancer account/payment settings UI"
        date = "2026-09-26 16:54:41 +0530"
        paths = @(
            "client/src/features/client/AccountSettings.jsx",
            "client/src/features/freelancer/PaymentSettings.jsx",
            "client/src/features/freelancer/components/EditPaymentModal.jsx",
            "client/src/features/shared/NotificationPreferences.jsx"
        )
    },
    @{
        msg = "feat(projects): implement client project creation and management UI"
        date = "2026-09-26 17:29:18 +0530"
        paths = @(
            "client/src/features/client/projectManagement/"
        )
    },
    @{
        msg = "feat(projects): implement freelancer find jobs and job details UI"
        date = "2026-09-26 18:06:57 +0530"
        paths = @(
            "client/src/features/freelancer/findJobs/",
            "client/src/features/freelancer/jobdetails/"
        )
    },
    @{
        msg = "feat(mobile): add category and skill models and services"
        date = "2026-09-27 16:05:33 +0530"
        paths = @(
            "mobile/lib/models/category_model.dart",
            "mobile/lib/models/skill_model.dart",
            "mobile/lib/services/category_dropdown_service.dart",
            "mobile/lib/services/skill_dropdown_service.dart",
            "mobile/lib/utils/components/category_dropdown.dart",
            "mobile/lib/utils/components/skill_dropdown.dart"
        )
    },
    @{
        msg = "feat(mobile): add profile models and services"
        date = "2026-09-27 16:38:12 +0530"
        paths = @(
            "mobile/lib/models/profile_info_model.dart",
            "mobile/lib/models/freelancer_profile_model.dart",
            "mobile/lib/models/profile_details_model.dart",
            "mobile/lib/services/profile_info_service.dart",
            "mobile/lib/services/profile_details_service.dart",
            "mobile/lib/services/profile_edit_service.dart"
        )
    },
    @{
        msg = "feat(mobile): implement profile details and editing views"
        date = "2026-09-27 17:11:47 +0530"
        paths = @(
            "mobile/lib/views/profile_view/",
            "mobile/lib/views/profile_details_view/",
            "mobile/lib/views/profile_edit_view/",
            "mobile/lib/view_models/profile_edit_view_model.dart",
            "mobile/lib/view_models/profile_details_view_model/profile_details_view_model.dart"
        )
    },
    @{
        msg = "feat(mobile): implement settings and preferences views"
        date = "2026-09-27 17:46:29 +0530"
        paths = @(
            "mobile/lib/views/profile_settings_view/",
            "mobile/lib/views/payment_settings_view/",
            "mobile/lib/views/notification_preferences_view/",
            "mobile/lib/views/change_password_view/",
            "mobile/lib/services/payment_settings_service.dart",
            "mobile/lib/services/notification_preferences_service.dart",
            "mobile/lib/view_models/payment_settings_view_model/payment_settings_view_model.dart"
        )
    },
    @{
        msg = "feat(mobile): add project models and services"
        date = "2026-09-27 18:20:54 +0530"
        paths = @(
            "mobile/lib/models/project_list_model.dart",
            "mobile/lib/models/project_details_model.dart",
            "mobile/lib/services/project_list_service.dart",
            "mobile/lib/services/project_details_service.dart"
        )
    },
    @{
        msg = "feat(mobile): implement project browsing and details views"
        date = "2026-09-27 18:57:36 +0530"
        paths = @(
            "mobile/lib/views/home_view/",
            "mobile/lib/views/fixed_price_project_details_view/",
            "mobile/lib/view_models/home_view_model/home_view_model.dart",
            "mobile/lib/view_models/my_projects_view_model/my_projects_view_model.dart"
        )
    },
    @{
        msg = "test: add unit tests for categories, profiles, portfolio, and projects"
        date = "2026-09-27 19:34:08 +0530"
        paths = @(
            "Tests/WebOfWork.Api.Tests/Categories/CategoryTests.cs",
            "Tests/WebOfWork.Api.Tests/Profiles/ProfileTests.cs",
            "Tests/WebOfWork.Api.Tests/Profiles/OnboardingTests.cs",
            "Tests/WebOfWork.Api.Tests/Profiles/SkillPackageCrudTests.cs",
            "Tests/WebOfWork.Api.Tests/Portfolio/PortfolioTests.cs",
            "Tests/WebOfWork.Api.Tests/Projects/ProjectTests.cs",
            "Tests/WebOfWork.Api.Tests/Projects/ProjectSkillNameTests.cs"
        )
    }
)

foreach ($c in $commits) {
    Write-Host "Processing commit: $($c.msg)"
    $hasChanges = $false
    foreach ($p in $c.paths) {
        $sourcePath = Join-Path $src $p
        $destPath = Join-Path $dest $p
        
        if (Test-Path $sourcePath) {
            $destDir = Split-Path $destPath -Parent
            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            
            Copy-Item -Path $sourcePath -Destination $destPath -Recurse -Force
            git add $p
            $hasChanges = $true
        } else {
            Write-Host "WARNING: Source path not found: $sourcePath"
        }
    }
    
    if ($hasChanges) {
        # Check if git actually has staged changes
        $status = git status --porcelain
        if ($status -match '^[AMDRC]') {
            $env:GIT_AUTHOR_DATE = $c.date
            $env:GIT_COMMITTER_DATE = $c.date
            git commit -m $c.msg
        } else {
            Write-Host "No new changes to commit for: $($c.msg)"
        }
    }
}
Write-Host "Script completed successfully."
