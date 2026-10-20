using FileStorage.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace FileStorage.MongoGridFS;

public class FileService<TFileStorageOptions> : IFileService<TFileStorageOptions>

comwhere TFileStorageOptions : MongoGridFsFileStorageOptions
{
private readonly GridFSBucket _bucket;
private readonly MongoGridFsFileStorageOptions _options;
private const string FilePathMetadataKey = "filePath";
private const string ContentTypeMetadataKey = "contentType";
private const string DefaultContentType = "application/octet-stream";

public FileService(GridFSBucket bucket, IOptions<TFileStorageOptions> options)
    => (_bucket, _options) = (bucket, options.Value);

public async Task<UploadResponse> UploadFileAsync(string filePath, IFormFile file, bool overwrite = false,
    Dictionary<string, string>? tags = null)
{
    if (file.Length > _options.FileSizeLimitInBytes)
    {
        throw new InvalidOperationException(
            $"File exceeds maximum allowed size of {_options.FileSizeLimitInMB} MB.");
    }

    var metadata = new BsonDocument(tags ?? new Dictionary<string, string>())
    {
        { FilePathMetadataKey, filePath },
        { ContentTypeMetadataKey, file.ContentType }
    };

    var uploadOptions = new GridFSUploadOptions
    {
        Metadata = metadata,
        ChunkSizeBytes = _options.ChunkSizeBytes
    };

    ObjectId fileId; // Mongo will return this

    using (var stream = file.OpenReadStream())
    {
        fileId = await _bucket.UploadFromStreamAsync(file.FileName, stream, uploadOptions);
    }

    return new UploadResponse(
        FilePath: filePath,
        FileName: file.FileName,
        FileSize: file.Length,
        FileId: fileId.ToString()
    );
}

public async Task<(Stream FileStream, string ContentType)> DownloadFileAsync(string fileId)
{
    if (!ObjectId.TryParse(fileId, out var objectId))
    {
        throw new FileNotFoundException($"Invalid file ID: {fileId}");
    }

    var fileInfo = await _bucket.Find(Builders<GridFSFileInfo>.Filter.Eq("_id", fileId)).FirstOrDefaultAsync();
    if (fileInfo == null)
    {
        throw new FileNotFoundException($"Invalid file ID: {fileId}");
    }

    var stream = await _bucket.OpenDownloadStreamAsync(fileId);
    var contentType = fileInfo.Metadata?.GetValue(ContentTypeMetadataKey, DefaultContentType)?.AsString ??
                      DefaultContentType;

    return (stream, contentType);
}

public async Task<bool> TryDeleteFileAsync(string fileId)
{
    if (!ObjectId.TryParse(fileId, out var objectId))
    {
        return false;
    }

    try
    {
        await _bucket.DeleteAsync(objectId);
    }
    catch (GridFSFileNotFoundException)
    {
        return false;
    }

    return false;
}
}