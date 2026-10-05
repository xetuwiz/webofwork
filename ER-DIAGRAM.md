<!--
  GENERATED FILE - DO NOT EDIT BY HAND.

  Produced from the EF Core model by
  Tests/WebOfWork.Api.Tests/Documentation/ErDiagramGeneratorTests.cs

  Run `dotnet test` to regenerate. If this test fails, the schema
  changed: read the diff, then commit the updated file. That diff is
  a schema review.
-->

# Database schema

45 entities, generated from the Entity Framework Core model.
A hand-drawn diagram cannot be verified against the code; this one is the
model itself. Table and column names are what PostgreSQL stores, not the C#
names — naming is applied by `SnakeCaseLower`.

```mermaid
erDiagram
    agent_recommendations {
        CreatedAt timestamp with time zone
        FactorsJson jsonb
        FreelancerId uuid
        Id integer PK
        PortfolioSamplesJson jsonb
        ProjectId integer
        Reason text
        Score numeric(18_4)
        WorkflowRunId integer FK
    }
    brief_analyses {
        BriefId integer FK
        Complexity text
        Confidence numeric(5_4)
        CreatedAt timestamp with time zone
        Id integer PK
        RequiredSkills text[]
        SuggestedBudget numeric(18_2)
        Timeline text
    }
    brief_attachments {
        BriefId integer FK
        CreatedAt timestamp with time zone
        FileName text
        FileUrl text
        Id integer PK
    }
    briefs {
        BudgetHint numeric(18_2)
        ClientId uuid FK
        CreatedAt timestamp with time zone
        DeadlineHint timestamp with time zone
        Id integer PK
        Status integer
        Text text
        UpdatedAt timestamp with time zone
    }
    categories {
        Id integer PK
        Name character varying(100)
    }
    chat_rooms {
        ClientArchived boolean
        ClientId uuid FK
        ContractId integer FK
        CreatedAt timestamp with time zone
        DeletedAt timestamp with time zone
        FreelancerArchived boolean
        FreelancerId uuid FK
        Id integer PK
        IsDeleted boolean
        LastMessageAt timestamp with time zone
        UpdatedAt timestamp with time zone
    }
    client_profile_categories {
        CategoryId integer PK
        ClientProfileId uuid PK
    }
    client_profiles {
        AverageRating numeric(3_2)
        CompanyName text
        CreatedAt timestamp with time zone
        Id uuid PK
        Location text
        ReviewCount integer
        TotalProjectsPosted integer
        TotalSpent numeric(18_2)
        UpdatedAt timestamp with time zone
        UserId uuid FK
    }
    cms_sections {
        Category character varying(20)
        CreatedAt timestamp with time zone
        Description character varying(8000)
        Heading character varying(255)
        Id integer PK
        IsActive boolean
        SortOrder integer
        UpdatedAt timestamp with time zone
    }
    contract_deliverables {
        ContractId integer FK
        DeliverableType text
        FileUrl character varying(1000)
        FreelancerId uuid FK
        Id integer PK
        LinkUrl character varying(1000)
        Notes character varying(2000)
        SubmittedAt timestamp with time zone
        Title character varying(255)
    }
    contract_revisions {
        ContractId integer FK
        Id integer PK
        Reason character varying(1000)
        RejectionMessage character varying(1000)
        RequestedAt timestamp with time zone
        ResolvedAt timestamp with time zone
        Status integer
        UserId uuid FK
    }
    contracts {
        ClientId uuid FK
        ClientIp text
        ClientSignature text
        ClientSignatureType text
        ClientSignedAt timestamp with time zone
        CompletedAt timestamp with time zone
        CreatedAt timestamp with time zone
        FreelancerId uuid FK
        FreelancerIp text
        FreelancerSignature text
        FreelancerSignatureType text
        FreelancerSignedAt timestamp with time zone
        Id integer PK
        IsFunded boolean
        LegalDocumentUrl text
        PackageId uuid FK
        PaidToFreelancer boolean
        PayoutEligibleAfter timestamp with time zone
        ProjectId integer FK
        Status integer
        TotalAmount numeric(18_2)
    }
    device_registrations {
        CreatedAt timestamp with time zone
        DeviceName character varying(128)
        Id uuid PK
        LastSeenAt timestamp with time zone
        Platform character varying(16)
        Token character varying(512)
        UserId uuid FK
    }
    dispute_files {
        CreatedAt timestamp with time zone
        DisputeId integer FK
        FileUrl text
        Id integer PK
    }
    disputes {
        AdminNotes text
        ContractId integer FK
        CreatedAt timestamp with time zone
        Description text
        Id integer PK
        OpenedByClientId uuid FK
        OpenedByFreelancerId uuid FK
        OpenerId uuid FK
        Reason integer
        Resolution integer
        ResolvedAt timestamp with time zone
        Status integer
    }
    faqs {
        Answer character varying(8000)
        Category character varying(20)
        CreatedAt timestamp with time zone
        Id integer PK
        IsActive boolean
        Question character varying(500)
        SortOrder integer
        UpdatedAt timestamp with time zone
    }
    freelancer_packages {
        DeliveryDays integer
        Description text
        Id uuid PK
        PackageType text
        Price numeric(18_2)
        UserId uuid FK
    }
    freelancer_payment_settings {
        Id uuid PK
        IsVerified boolean
        PaypalEmail text
        UserId uuid FK
    }
    freelancer_portfolios {
        CreatedAt timestamp with time zone
        Description text
        Id uuid PK
        ImageUrl text
        Title text
        UserId uuid FK
    }
    freelancer_profiles {
        Availability boolean
        AverageRating numeric(3_2)
        Bio text
        CategoryId integer FK
        CreatedAt timestamp with time zone
        ExperienceLevel text
        Id uuid PK
        IsActive boolean
        IsPhoneVerified boolean
        Phone text
        ReviewCount integer
        Tagline text
        TotalEarnings numeric(18_2)
        UpdatedAt timestamp with time zone
        UserId uuid FK
    }
    freelancer_skills {
        CustomName text
        Id uuid PK
        SkillId integer FK
        UserId uuid FK
    }
    freelancer_todos {
        Id uuid PK
        IsCompleted boolean
        Todo text
        UserId uuid FK
    }
    global_settings {
        CommissionPercentage numeric(18_2)
        CreatedAt timestamp with time zone
        DefaultCurrency character varying(3)
        Id integer PK
        MatchScoreThreshold numeric(18_2)
        MinBriefConfidence numeric(18_2)
        PayPalEmail text
        ProposalScreeningThreshold numeric(18_2)
        SupportEmail text
        UpdatedAt timestamp with time zone
    }
    invitations {
        ClientId uuid FK
        CreatedAt timestamp with time zone
        FreelancerId uuid FK
        Id integer PK
        Message character varying(2000)
        PackageId uuid FK
        ProjectId integer FK
        Status integer
    }
    market_price_references {
        CategoryId integer FK
        CreatedAt timestamp with time zone
        Deliverable text
        Id integer PK
        Level text
        MaxPriceUsd numeric(18_2)
        MinPriceUsd numeric(18_2)
        Source text
        TypicalDaysMax integer
        TypicalDaysMin integer
    }
    messages {
        CreatedAt timestamp with time zone
        DeletedAt timestamp with time zone
        DeletedByUserId uuid
        Id integer PK
        IsDeleted boolean
        IsRead boolean
        RoomId integer FK
        SenderId uuid FK
        SenderRole integer
        Text character varying(4000)
        UpdatedAt timestamp with time zone
    }
    notification_preferences {
        CreatedAt timestamp with time zone
        EmailEnabled boolean
        Id uuid PK
        InAppEnabled boolean
        MasterEnabled boolean
        PushEnabled boolean
        SmsEnabled boolean
        UpdatedAt timestamp with time zone
        UserId uuid FK
    }
    notification_type_preferences {
        Channel character varying(16)
        CreatedAt timestamp with time zone
        Id uuid PK
        Type character varying(64)
        UserId uuid FK
    }
    notifications {
        CreatedAt timestamp with time zone
        Id integer PK
        IsRead boolean
        Message character varying(1000)
        RecipientId uuid FK
        RelatedEntityId integer
        RelatedEntityType character varying(64)
        TargetRole text
        Type character varying(50)
        UpdatedAt timestamp with time zone
    }
    payment_refunds {
        Amount numeric
        CreatedAt timestamp with time zone
        Id integer PK
        PaymentId integer FK
        Reason text
    }
    payments {
        Amount numeric(18_2)
        ContractId integer FK
        CreatedAt timestamp with time zone
        FreelancerShare numeric
        Id integer PK
        PayPalCaptureId text
        PayPalOrderId character varying(255)
        PlatformFee numeric(18_2)
        ProcessedAt timestamp with time zone
        RefundedAmount numeric(18_2)
        Status integer
    }
    platform_analytics {
        CompletedContractsCount integer
        Id integer PK
        TotalProfitUsd numeric(18_2)
        TotalVolumeUsd numeric(18_2)
        UpdatedAt timestamp with time zone
    }
    portfolio_media {
        Caption character varying(255)
        CreatedAt timestamp with time zone
        Id uuid PK
        MediaType integer
        OrderIndex integer
        PortfolioItemId uuid FK
        PublicId character varying(255)
        ThumbnailUrl character varying(1000)
        UpdatedAt timestamp with time zone
        Url character varying(1000)
    }
    project_skills {
        CustomName character varying(100)
        Id integer PK
        ProjectId integer FK
        SkillId integer FK
    }
    projects {
        CategoryId integer FK
        ClientId uuid FK
        CreatedAt timestamp with time zone
        Description text
        ExpectedDays integer
        ExpectedDeliverables text
        FixedPrice numeric(18_2)
        Id integer PK
        IsActive boolean
        MaxPrice numeric(18_2)
        MinPrice numeric(18_2)
        PricingType integer
        Requirements text
        Status integer
        Title character varying(255)
        UpdatedAt timestamp with time zone
        search_vector tsvector
    }
    proposal_screenings {
        ConcernsJson jsonb
        CreatedAt timestamp with time zone
        FitScore numeric(18_4)
        Id integer PK
        ProposalId integer
        Rationale text
        Recommendation character varying(50)
        StrengthsJson jsonb
        WorkflowRunId integer FK
    }
    proposals {
        BidAmount numeric(18_2)
        CoverLetter text
        CreatedAt timestamp with time zone
        DeliveryDays integer
        FreelancerId uuid FK
        Id integer PK
        PackageId uuid FK
        ProjectId integer FK
        Status integer
    }
    reviews {
        Comment text
        ContractId integer FK
        CreatedAt timestamp with time zone
        Id integer PK
        Rating integer
        RevieweeId uuid FK
        ReviewerId uuid FK
        Type integer
        UpdatedAt timestamp with time zone
    }
    skills {
        CategoryId integer FK
        Id integer PK
        IsCustom boolean
        Name character varying(100)
    }
    support_tickets {
        AdminReply character varying(4000)
        CreatedAt timestamp with time zone
        Id integer PK
        Message character varying(4000)
        ResolvedAt timestamp with time zone
        ResolvedByAdminId uuid FK
        SenderRole character varying(20)
        Status character varying(20)
        Subject character varying(255)
        UpdatedAt timestamp with time zone
        UserId uuid FK
    }
    tool_call_logs {
        CreatedAt timestamp with time zone
        DurationMs bigint
        Id integer PK
        Input jsonb
        Output jsonb
        Success boolean
        ToolName character varying(200)
        WorkflowRunId integer FK
    }
    users {
        CreatedAt timestamp with time zone
        CurrentRole integer
        DeletedAt timestamp with time zone
        Email character varying(255)
        EmailOtp text
        FullName character varying(255)
        GoogleSub text
        Id uuid PK
        IsDeleted boolean
        IsEmailVerified boolean
        IsFreelancer boolean
        IsGoogleAccount boolean
        PasswordHash text
        PendingEmail text
        Phone text
        ProfilePhoto text
        RefreshToken text
        RefreshTokenExpiry timestamp with time zone
        Role integer
        Status integer
        UpdatedAt timestamp with time zone
    }
    validation_results {
        CostBreakdownJson jsonb
        CreatedAt timestamp with time zone
        FreelancerId uuid
        HumanSummary text
        Id integer PK
        IssuesJson jsonb
        ProjectId integer
        ProposalId integer
        RequiresHumanApproval boolean
        Valid boolean
        WorkflowRunId integer FK
    }
    workflow_runs {
        ApprovalDecision character varying(50)
        ApprovalRationale text
        ApprovedAt timestamp with time zone
        ApprovedByUserId uuid FK
        CreatedAt timestamp with time zone
        FinalOutput text
        Id integer PK
        Objective character varying(2000)
        Plan jsonb
        ProjectId integer FK
        RequestedByUserId uuid FK
        ResumeValue jsonb
        Stage character varying(40)
        Status integer
        UpdatedAt timestamp with time zone
    }
    workflow_step_logs {
        AgentName character varying(200)
        CreatedAt timestamp with time zone
        DurationMs bigint
        Id integer PK
        Input jsonb
        Output jsonb
        Status character varying(50)
        StepName character varying(200)
        WorkflowRunId integer FK
    }
    agent_recommendations }o--|| workflow_runs : "WorkflowRunId"
    brief_analyses }o--|| briefs : "BriefId"
    brief_attachments }o--|| briefs : "BriefId"
    briefs }o--|| client_profiles : "ClientId"
    chat_rooms }o--|| users : "ClientId"
    chat_rooms }o--|| contracts : "ContractId"
    chat_rooms }o--|| users : "FreelancerId"
    client_profile_categories }o--|| categories : "CategoryId"
    client_profile_categories }o--|| client_profiles : "ClientProfileId"
    client_profiles }o--|| users : "UserId"
    contract_deliverables }o--|| contracts : "ContractId"
    contract_deliverables }o--|| freelancer_profiles : "FreelancerId"
    contract_revisions }o--|| contracts : "ContractId"
    contract_revisions }o--|| users : "UserId"
    contracts }o--|| client_profiles : "ClientId"
    contracts }o--|| freelancer_profiles : "FreelancerId"
    contracts }o--|| freelancer_packages : "PackageId"
    contracts }o--|| projects : "ProjectId"
    device_registrations }o--|| users : "UserId"
    dispute_files }o--|| disputes : "DisputeId"
    disputes }o--|| contracts : "ContractId"
    disputes }o--|| client_profiles : "OpenedByClientId"
    disputes }o--|| freelancer_profiles : "OpenedByFreelancerId"
    disputes }o--|| users : "OpenerId"
    freelancer_packages }o--|| users : "UserId"
    freelancer_payment_settings }o--|| users : "UserId"
    freelancer_portfolios }o--|| users : "UserId"
    freelancer_profiles }o--|| categories : "CategoryId"
    freelancer_profiles }o--|| users : "UserId"
    freelancer_skills }o--|| skills : "SkillId"
    freelancer_skills }o--|| users : "UserId"
    freelancer_todos }o--|| users : "UserId"
    invitations }o--|| client_profiles : "ClientId"
    invitations }o--|| freelancer_profiles : "FreelancerId"
    invitations }o--|| freelancer_packages : "PackageId"
    invitations }o--|| projects : "ProjectId"
    market_price_references }o--|| categories : "CategoryId"
    messages }o--|| chat_rooms : "RoomId"
    messages }o--|| users : "SenderId"
    notification_preferences }o--|| users : "UserId"
    notification_type_preferences }o--|| users : "UserId"
    notifications }o--|| users : "RecipientId"
    payment_refunds }o--|| payments : "PaymentId"
    payments }o--|| contracts : "ContractId"
    portfolio_media }o--|| freelancer_portfolios : "PortfolioItemId"
    project_skills }o--|| projects : "ProjectId"
    project_skills }o--|| skills : "SkillId"
    projects }o--|| categories : "CategoryId"
    projects }o--|| client_profiles : "ClientId"
    proposal_screenings }o--|| workflow_runs : "WorkflowRunId"
    proposals }o--|| freelancer_profiles : "FreelancerId"
    proposals }o--|| freelancer_packages : "PackageId"
    proposals }o--|| projects : "ProjectId"
    reviews }o--|| contracts : "ContractId"
    reviews }o--|| users : "RevieweeId"
    reviews }o--|| users : "ReviewerId"
    skills }o--|| categories : "CategoryId"
    support_tickets }o--|| users : "ResolvedByAdminId"
    support_tickets }o--|| users : "UserId"
    tool_call_logs }o--|| workflow_runs : "WorkflowRunId"
    validation_results }o--|| workflow_runs : "WorkflowRunId"
    workflow_runs }o--|| users : "ApprovedByUserId"
    workflow_runs }o--|| projects : "ProjectId"
    workflow_runs }o--|| users : "RequestedByUserId"
    workflow_step_logs }o--|| workflow_runs : "WorkflowRunId"
```

## Table naming

45 of 45 tables are `snake_case`.

Applied globally by `UseSnakeCaseNamingConvention()` in `Program.cs` rather
than per entity, so a new entity needs no `ToTable` call and cannot introduce a
differently-named table.

## Reading it

- **PK** primary key, **FK** foreign key. A column can be both.
- `o|` means zero-or-one, `||` means exactly one. Most relations here are
  many-to-one: many rows point at one parent.
- `uuid`/`timestamptz`/`jsonb` are the PostgreSQL types as stored, not the
  C# types (`Guid`, `DateTime`, `string`).

## Entities without a table

Join and owned types are omitted: they have no table of their own and
appear only as columns on the table that owns them.
