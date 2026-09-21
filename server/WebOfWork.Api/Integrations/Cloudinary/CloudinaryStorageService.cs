using System;
using System.IO;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace WebOfWork.Api.Integrations.Cloudinary;

public interface ICloudinaryStorageService
{
    Task<string> UploadPdfAsync(byte[] pdfBytes, string filename);
}

public class CloudinaryStorageService : ICloudinaryStorageService
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;

    public CloudinaryStorageService(IConfiguration config)
    {
        var account = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );
        _cloudinary = new CloudinaryDotNet.Cloudinary(account);
    }

    public async Task<string> UploadPdfAsync(byte[] pdfBytes, string filename)
    {
        using var stream = new MemoryStream(pdfBytes);
        var uploadParams = new RawUploadParams()
        {
            File = new FileDescription(filename, stream),
            Folder = "contracts",
            UseFilename = true,
            UniqueFilename = true
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
        
        if (uploadResult.Error != null)
        {
            throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
        }
        
        return uploadResult.SecureUrl.ToString();
    }
}
