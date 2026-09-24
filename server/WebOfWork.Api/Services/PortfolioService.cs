using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Portfolio;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public class PortfolioService : IPortfolioService
{
    private readonly WebOfWorkDbContext _context;
    private readonly Cloudinary _cloudinary;
    private readonly CloudinaryOptions _options;

    public PortfolioService(WebOfWorkDbContext context, IOptions<CloudinaryOptions> cloudinaryOptions)
    {
        _context = context;
        _options = cloudinaryOptions.Value;

        var account = new Account(
            _options.CloudName,
            _options.ApiKey,
            _options.ApiSecret
        );
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<PortfolioMediaResponse> UploadMediaAsync(Guid freelancerId, UploadPortfolioMediaRequest request)
    {
        var portfolio = await _context.FreelancerPortfolios
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioItemId && p.UserId == freelancerId);

        if (portfolio == null)
            throw new Exception("Portfolio item not found or unauthorized");

        await using var stream = request.File.OpenReadStream();

        var isVideo = request.File.ContentType.StartsWith("video");
        var isPdf = request.File.ContentType == "application/pdf";

        var uploadParams = new RawUploadParams()
        {
            File = new FileDescription(request.File.FileName, stream),

        };

        RawUploadResult uploadResult;
        MediaType mediaType;

        if (isVideo)
        {
            var videoParams = new VideoUploadParams()
            {
                File = new FileDescription(request.File.FileName, stream),

            };
            uploadResult = await _cloudinary.UploadAsync(videoParams);
            mediaType = MediaType.Video;
        }
        else if (isPdf)
        {
            uploadResult = await _cloudinary.UploadAsync(uploadParams);
            mediaType = MediaType.Document;
        }
        else
        {
            var imageParams = new ImageUploadParams()
            {
                File = new FileDescription(request.File.FileName, stream),

            };
            uploadResult = await _cloudinary.UploadAsync(imageParams);
            mediaType = MediaType.Image;
        }

        if (uploadResult.Error != null)
        {
            throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
        }

        var newMedia = new PortfolioMedia
        {
            PortfolioItemId = request.PortfolioItemId,
            MediaType = mediaType,
            Url = uploadResult.SecureUrl?.ToString() ?? string.Empty,
            PublicId = uploadResult.PublicId,
            Caption = request.Caption,
            OrderIndex = portfolio.Media.Count
        };

        if (mediaType == MediaType.Video)
        {
            var urlStr = newMedia.Url;
            var extIndex = urlStr.LastIndexOf('.');
            if (extIndex > -1)
            {
                newMedia.ThumbnailUrl = urlStr.Substring(0, extIndex) + ".jpg";
            }
        }
        else if (mediaType == MediaType.Image)
        {
            newMedia.ThumbnailUrl = newMedia.Url;
        }

        _context.PortfolioMedia.Add(newMedia);
        await _context.SaveChangesAsync();

        return new PortfolioMediaResponse(
            newMedia.Id,
            newMedia.PortfolioItemId,
            newMedia.MediaType,
            newMedia.Url,
            newMedia.ThumbnailUrl,
            newMedia.Caption,
            newMedia.OrderIndex
        );
    }

    public async Task DeleteMediaAsync(Guid freelancerId, Guid mediaId)
    {
        var media = await _context.PortfolioMedia
            .Include(m => m.PortfolioItem)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.PortfolioItem.UserId == freelancerId);

        if (media == null)
            throw new Exception("Media not found or unauthorized");

        if (!string.IsNullOrEmpty(media.PublicId))
        {
            var resourceType = media.MediaType == MediaType.Video ? ResourceType.Video : ResourceType.Image;
            var deletionParams = new DeletionParams(media.PublicId)
            {
                ResourceType = resourceType
            };
            await _cloudinary.DestroyAsync(deletionParams);
        }

        _context.PortfolioMedia.Remove(media);
        await _context.SaveChangesAsync();
    }

    public async Task ReorderMediaAsync(Guid freelancerId, Guid portfolioItemId, List<Guid> mediaIds)
    {
        var mediaList = await _context.PortfolioMedia
            .Include(m => m.PortfolioItem)
            .Where(m => m.PortfolioItemId == portfolioItemId && m.PortfolioItem.UserId == freelancerId)
            .ToListAsync();

        if (mediaList.Count == 0) return;

        for (int i = 0; i < mediaIds.Count; i++)
        {
            var media = mediaList.FirstOrDefault(m => m.Id == mediaIds[i]);
            if (media != null)
            {
                media.OrderIndex = i;
            }
        }

        await _context.SaveChangesAsync();
    }

}
