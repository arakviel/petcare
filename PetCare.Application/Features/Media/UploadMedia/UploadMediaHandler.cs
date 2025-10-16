﻿namespace PetCare.Application.Features.Media.UploadMedia;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PetCare.Application.Interfaces;

/// <summary>
/// Handles uploading media files and returning their URL.
/// Media type is automatically determined by file extension.
/// Validates file size and extension before uploading.
/// </summary>
public class UploadMediaHandler : IRequestHandler<UploadMediaCommand, string>
{
    /// <summary>
    /// Maximum allowed photo size in bytes (5 MB).
    /// </summary>
    private const long MaxPhotoSize = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum allowed video size in bytes (50 MB).
    /// </summary>
    private const long MaxVideoSize = 50 * 1024 * 1024;

    /// <summary>
    /// Supported photo file extensions.
    /// </summary>
    private static readonly string[] PhotoExtensions = new[]
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff",
    };

    /// <summary>
    /// Supported video file extensions.
    /// </summary>
    private static readonly string[] VideoExtensions = new[]
    {
        ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".mpeg",
    };

    private readonly IStorageService storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadMediaHandler"/> class.
    /// </summary>
    /// <param name="storageService">The file storage service used to save uploaded files.</param>
    public UploadMediaHandler(IStorageService storageService)
    {
        this.storageService = storageService ?? throw new ArgumentNullException(nameof(storageService), "Сервіс збереження файлів не може бути null.");
    }

    /// <summary>
    /// Handles the media upload command.
    /// Determines media type based on file extension, validates size and extension, and uploads the file.
    /// </summary>
    /// <param name="request">The upload media command containing the file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The URL of the uploaded media file.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the file is empty, has an invalid extension, or exceeds the maximum allowed size.
    /// </exception>
    public async Task<string> Handle(UploadMediaCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            throw new ArgumentException("Файл не може бути порожнім.");
        }

        var extension = Path.GetExtension(request.File.FileName)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Файл має містити розширення.");
        }

        string mediaType;
        long maxSizeBytes;
        string[] allowedExtensions;

        if (PhotoExtensions.Contains(extension))
        {
            mediaType = "photo";
            maxSizeBytes = MaxPhotoSize;
            allowedExtensions = PhotoExtensions;
        }
        else if (VideoExtensions.Contains(extension))
        {
            mediaType = "video";
            maxSizeBytes = MaxVideoSize;
            allowedExtensions = VideoExtensions;
        }
        else
        {
            throw new ArgumentException($"Недопустимий формат файлу. Дозволені формати фото: {string.Join(", ", PhotoExtensions)}, відео: {string.Join(", ", VideoExtensions)}");
        }

        if (request.File.Length > maxSizeBytes)
        {
            throw new ArgumentException($"Файл перевищує максимальний розмір {maxSizeBytes / (1024 * 1024)} MB для {mediaType}.");
        }

        await using var stream = request.File.OpenReadStream();
        var url = await this.storageService.UploadFileAsync(request.File.FileName, stream, request.File.ContentType);
        return url;
    }
}
