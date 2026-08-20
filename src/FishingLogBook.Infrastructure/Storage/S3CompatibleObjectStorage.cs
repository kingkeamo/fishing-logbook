using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.Config;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Infrastructure.Storage;

public sealed class S3CompatibleObjectStorage : IObjectStorage, IDisposable
{
    private readonly ObjectStorageConfig _config;
    private readonly AmazonS3Client? _client;

    public S3CompatibleObjectStorage(IOptions<ObjectStorageConfig> configOptions)
    {
        _config = configOptions.Value;
        if (!_config.IsConfigured)
        {
            return;
        }

        var credentials = new BasicAWSCredentials(_config.AccessKeyId, _config.SecretAccessKey);
        var amazonS3Config = new AmazonS3Config
        {
            ServiceURL = _config.ServiceUrl.TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        };
        _client = new AmazonS3Client(credentials, amazonS3Config);
    }

    public bool IsConfigured => _config.IsConfigured && _client is not null;

    public Task<Uri> CreateUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(lifetime),
            ContentType = contentType
        };

        return Task.FromResult(new Uri(Client.GetPreSignedURL(request)));
    }

    public Task<Uri> CreateDownloadUrlAsync(string objectKey, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime)
        };

        return Task.FromResult(new Uri(Client.GetPreSignedURL(request)));
    }

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        return Client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _config.BucketName,
                Key = objectKey
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private AmazonS3Client Client =>
        _client ?? throw new InvalidOperationException("Object storage is not configured.");
}
