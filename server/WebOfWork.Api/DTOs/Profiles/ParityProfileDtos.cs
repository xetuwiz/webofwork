using System;
using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Profiles;

public record ParityClientProfileResponse(Guid Id, string FullName, string Email, string? ProfilePhoto, string CompanyName, string Location, IReadOnlyList<string> Categories, DateTime JoinedAt, bool IsGoogleAccount, int ReviewCount, decimal AverageRating);
public record FreelancerSmallResponse(Guid Id, string Tagline, string Bio, string Phone, bool IsPhoneVerified, int? Category, string ExperienceLevel, bool Availability, decimal AverageRating, int ReviewCount);
public record ProfilePackageResponse(Guid Id, decimal Price, int DeliveryDays, string[] Description);
public record ProfilePackageTiers(ProfilePackageResponse? Starter, ProfilePackageResponse? Pro);
public record ProfilePortfolioResponse(string Title, string Description, string ImageUrl);
public record ProfilePaymentResponse(string PaypalEmail, bool IsVerified);
public record FreelancerBigResponse(string FullName, string? ProfilePhoto, string Tagline, string Bio, string Phone, int? Category, string ExperienceLevel, bool Availability, IReadOnlyList<string> Skills, ProfilePackageTiers Packages, IReadOnlyList<ProfilePortfolioResponse> Portfolios, ProfilePaymentResponse? PaymentSettings, DateTime CreatedAt, int CompletedProjectsCount, int ReviewCount, decimal AverageRating, IReadOnlyList<object> Reviews);
public record ParityFreelancerListItemResponse(Guid Id, string FullName, string? ProfilePhoto, string Tagline, string Bio, string? CategoryName, string ExperienceLevel, IReadOnlyList<string> Skills, decimal StartingPrice, bool Availability, decimal AverageRating, int ReviewCount);
public record SwitchRoleRequest(string Role);
public record PaymentSettingsRequest(string PaypalEmail);
public record TodoRequest(string Todo, bool IsCompleted = false);
public record ReviewCreateRequest(int Contract, Guid Reviewee, string ReviewType, int Rating, string Comment);
