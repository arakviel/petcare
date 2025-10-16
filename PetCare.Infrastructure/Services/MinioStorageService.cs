﻿namespace PetCare.Infrastructure.Services;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using PetCare.Application.Interfaces;

/// <summary>
/// MinIO-based implementation of IStorageService.
/// </summary>
public sealed class MinioStorageService : IStorageService
{
    private readonly IMinioClient minioClient;
    private readonly ILogger<MinioStorageService> logger;
    private readonly string bucketName;
    private readonly string endpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="MinioStorageService"/> class.
    /// Sets up the connection to MinIO and validates required configuration parameters.
    /// </summary>
    /// <param name="configuration">The application configuration from which MinIO settings are read.</param>
    /// <param name="logger">The logger used to record informational and error messages.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if any of the required MinIO configuration parameters are not set:
    /// <c>MINIO_ENDPOINT</c>, <c>MINIO_ROOT_USER</c>, <c>MINIO_ROOT_PASSWORD</c>, <c>MINIO_BUCKET_NAME</c>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the provided <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public MinioStorageService(IConfiguration configuration, ILogger<MinioStorageService> logger)
    {
        this.endpoint = configuration["MINIO_ENDPOINT"]
           ?? throw new InvalidOperationException("MINIO_ENDPOINT не налаштований.");
        var accessKey = configuration["MINIO_ROOT_USER"]
            ?? throw new InvalidOperationException("MINIO_ROOT_USER не налаштований.");
        var secretKey = configuration["MINIO_ROOT_PASSWORD"]
            ?? throw new InvalidOperationException("MINIO_ROOT_PASSWORD не налаштований.");
        this.bucketName = configuration["MINIO_BUCKET_NAME"]
            ?? throw new InvalidOperationException("MINIO_BUCKET_NAME не налаштований.");

        this.logger = logger ?? throw new ArgumentNullException(nameof(logger), "Логер не може бути null.");

        this.minioClient = new MinioClient()
            .WithEndpoint(this.endpoint.Replace("http://", string.Empty)) // e.g. minio:9000
            .WithCredentials(accessKey, secretKey)
            .WithSSL(false)
            .Build();
    }

    /// <inheritdoc/>
    public async Task<string> UploadFileAsync(string objectName, Stream data, string contentType)
    {
        try
        {
            await this.EnsureBucketExistsAsync();

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(this.bucketName)
                .WithObject(objectName)
                .WithStreamData(data)
                .WithObjectSize(data.Length)
                .WithContentType(contentType);

            await this.minioClient.PutObjectAsync(putObjectArgs).ConfigureAwait(false);

            var fileUrl = $"{this.endpoint}/{this.bucketName}/{objectName}";
            this.logger.LogInformation("Uploaded file '{Object}' to MinIO bucket '{Bucket}'", objectName, this.bucketName);

            return fileUrl;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to upload file '{Object}'", objectName);
            throw new InvalidOperationException($"Не вдалося завантажити файл '{objectName}'.");
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadFileAsync(string objectName)
    {
        try
        {
            var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(this.bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await this.minioClient.GetObjectAsync(getObjectArgs).ConfigureAwait(false);

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to download file '{Object}'", objectName);
            throw new InvalidOperationException($"Не вдалося завантажити файл '{objectName}'.");
        }
    }

    /// <inheritdoc/>
    public async Task DeleteFileAsync(string objectName)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(this.bucketName)
                .WithObject(objectName);

            await this.minioClient.RemoveObjectAsync(args).ConfigureAwait(false);
            this.logger.LogInformation("Deleted file '{Object}' from bucket '{Bucket}'", objectName, this.bucketName);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to delete file '{Object}'", objectName);
            throw new InvalidOperationException($"Не вдалося видалити файл '{objectName}'.");
        }
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePresignedUrlAsync(string objectName, int expirySeconds = 3600)
    {
        try
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(this.bucketName)
                .WithObject(objectName)
                .WithExpiry(expirySeconds);

            var url = await this.minioClient.PresignedGetObjectAsync(args).ConfigureAwait(false);
            return url;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to generate presigned URL for '{Object}'", objectName);
            throw new InvalidOperationException($"Не вдалося створити тимчасове посилання для '{objectName}'.");
        }
    }

    private async Task EnsureBucketExistsAsync()
    {
        var existsArgs = new BucketExistsArgs().WithBucket(this.bucketName);
        var exists = await this.minioClient.BucketExistsAsync(existsArgs).ConfigureAwait(false);

        if (!exists)
        {
            await this.minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(this.bucketName)).ConfigureAwait(false);
            this.logger.LogInformation("Created bucket '{Bucket}'", this.bucketName);
        }
    }
}
