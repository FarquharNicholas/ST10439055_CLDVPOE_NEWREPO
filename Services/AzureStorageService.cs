using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using ST10439055_CLDVPOE.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs.Models;

namespace ST10439055_CLDVPOE.Services
{
    public class AzureStorageService : IAzureStorageService
    {
        private readonly TableServiceClient _tableServiceClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly QueueServiceClient _queueServiceClient;
        private readonly ShareServiceClient? _shareServiceClient;
        private readonly ILogger<AzureStorageService> _logger;

        public AzureStorageService(
            IConfiguration configuration,
            ILogger<AzureStorageService> logger)
        {
            string connectionString = configuration.GetConnectionString("AzureStorage")
                ?? throw new InvalidOperationException("Azure Storage connection string not found");

            try
            {
                _tableServiceClient = new TableServiceClient(connectionString);
                _blobServiceClient = new BlobServiceClient(connectionString);
                _queueServiceClient = new QueueServiceClient(connectionString);
                
                // Initialize File Share service only if not using development storage
                // Development storage doesn't support File Shares properly
                if (!connectionString.Contains("UseDevelopmentStorage=true"))
                {
                    try
                    {
                        _shareServiceClient = new ShareServiceClient(connectionString);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "File Share service initialization failed: {Message}. File operations will be disabled.", ex.Message);
                        _shareServiceClient = null;
                    }
                }
                else
                {
                    logger?.LogInformation("Development storage detected - File Share service disabled");
                    _shareServiceClient = null;
                }
                
                _logger = logger;

                // Initialize storage asynchronously without blocking the constructor
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await InitializeStorageAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to initialize Azure Storage: {Message}", ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to create Azure Storage clients: {Message}", ex.Message);
                throw;
            }
        }

        private async Task InitializeStorageAsync()
        {
            try
            {
                _logger?.LogInformation("Starting Azure Storage initialization...");

                // Create tables
                await _tableServiceClient.CreateTableIfNotExistsAsync("Customers");
                await _tableServiceClient.CreateTableIfNotExistsAsync("Products");
                await _tableServiceClient.CreateTableIfNotExistsAsync("Orders");
                _logger?.LogInformation("Tables created successfully");

                // Create blob containers with retry logic
                var productImagesContainer = _blobServiceClient.GetBlobContainerClient("product-images");
                await productImagesContainer.CreateIfNotExistsAsync(PublicAccessType.Blob);
                var paymentProofsContainer = _blobServiceClient.GetBlobContainerClient("payment-proofs");
                await paymentProofsContainer.CreateIfNotExistsAsync(PublicAccessType.None);
                _logger?.LogInformation("Blob containers created successfully");

                // Create queues
                var orderQueue = _queueServiceClient.GetQueueClient("order-notifications");
                await orderQueue.CreateIfNotExistsAsync();
                var stockQueue = _queueServiceClient.GetQueueClient("stock-updates");
                await stockQueue.CreateIfNotExistsAsync();
                _logger?.LogInformation("Queues created successfully");

                // Create file share only if service is available
                if (_shareServiceClient != null)
                {
                    try
                    {
                        var contractsShare = _shareServiceClient.GetShareClient("contracts");
                        await contractsShare.CreateIfNotExistsAsync();
                        // Create payments directory in contracts share
                        var contractsDirectory = contractsShare.GetDirectoryClient("payments");
                        await contractsDirectory.CreateIfNotExistsAsync();
                        _logger?.LogInformation("File shares created successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "File share initialization failed: {Message}", ex.Message);
                    }
                }
                else
                {
                    _logger?.LogInformation("File share service not available - skipping file share initialization");
                }

                _logger?.LogInformation("Azure Storage initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize Azure Storage: {Message}", ex.Message);
                throw; // Re-throw to make the error visible
            }
        }

        // Table Operations
        public async Task<List<T>> GetAllEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            var tableName = GetTableName<T>();
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            var entities = new List<T>();
            await foreach (var entity in tableClient.QueryAsync<T>())
            {
                entities.Add(entity);
            }
            return entities;
        }

        public async Task<T?> GetEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            var tableName = GetTableName<T>();
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            if (string.IsNullOrWhiteSpace(partitionKey) || string.IsNullOrWhiteSpace(rowKey))
            {
                _logger?.LogWarning("GetEntityAsync called with invalid keys for {EntityType}. PartitionKey or RowKey was null/empty.", typeof(T).Name);
                return null;
            }
            try
            {
                var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<T> AddEntityAsync<T>(T entity) where T : class, ITableEntity
        {
            var tableName = GetTableName<T>();
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            await tableClient.AddEntityAsync(entity);
            return entity;
        }

        public async Task<T> UpdateEntityAsync<T>(T entity) where T : class, ITableEntity
        {
            var tableName = GetTableName<T>();
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            try
            {
                // Use IfMatch condition for optimistic concurrency
                await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
                return entity;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 412)
            {
                // Precondition failed – entity was modified by another process
                _logger?.LogWarning("Entity update failed due to ETag mismatch for {EntityType} with RowKey {RowKey}", typeof(T).Name, entity.RowKey);
                throw new InvalidOperationException("The entity was modified by another process. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating entity {EntityType} with RowKey {RowKey}: {Message}", typeof(T).Name, entity.RowKey, ex.Message);
                throw;
            }
        }

        public async Task DeleteEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            var tableName = GetTableName<T>();
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
        }

        // Blob Operations
        public async Task<string> UploadImageAsync(IFormFile file, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                // Ensure container exists
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var blobClient = containerClient.GetBlobClient(fileName);
                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, overwrite: true);
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error uploading image to container {ContainerName}: {Message}", containerName, ex.Message);
                throw;
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                // Ensure container exists
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{file.FileName}";
                var blobClient = containerClient.GetBlobClient(fileName);
                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, overwrite: true);
                return fileName;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error uploading file to container {ContainerName}: {Message}", containerName, ex.Message);
                throw;
            }
        }

        public async Task DeleteBlobAsync(string blobName, string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        // Queue Operations
        public async Task SendMessageAsync(string queueName, string message)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            await queueClient.SendMessageAsync(message);
        }

        public async Task<string?> ReceiveMessageAsync(string queueName)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            var response = await queueClient.ReceiveMessageAsync();
            if (response.Value != null)
            {
                await queueClient.DeleteMessageAsync(response.Value.MessageId, response.Value.PopReceipt);
                return response.Value.MessageText;
            }
            return null;
        }

        // File Share Operations
        public async Task<string> UploadToFileShareAsync(IFormFile file, string shareName, string directoryName = "")
        {
            if (_shareServiceClient == null)
            {
                throw new InvalidOperationException("File Share service is not available. This feature is not supported in development storage mode.");
            }

            var shareClient = _shareServiceClient.GetShareClient(shareName);
            var directoryClient = string.IsNullOrEmpty(directoryName) ? shareClient.GetRootDirectoryClient() : shareClient.GetDirectoryClient(directoryName);
            await directoryClient.CreateIfNotExistsAsync();
            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{file.FileName}";
            var fileClient = directoryClient.GetFileClient(fileName);
            using var stream = file.OpenReadStream();
            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadAsync(stream);
            return fileName;
        }

        public async Task<byte[]> DownloadFromFileShareAsync(string shareName, string fileName, string directoryName = "")
        {
            if (_shareServiceClient == null)
            {
                throw new InvalidOperationException("File Share service is not available. This feature is not supported in development storage mode.");
            }

            var shareClient = _shareServiceClient.GetShareClient(shareName);
            var directoryClient = string.IsNullOrEmpty(directoryName) ? shareClient.GetRootDirectoryClient() : shareClient.GetDirectoryClient(directoryName);
            var fileClient = directoryClient.GetFileClient(fileName);
            var response = await fileClient.DownloadAsync();
            using var memoryStream = new MemoryStream();
            await response.Value.Content.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private static string GetTableName<T>()
        {
            return typeof(T).Name switch
            {
                nameof(Customer) => "Customers",
                nameof(Product) => "Products",
                nameof(Order) => "Orders",
                _ => typeof(T).Name + "s"
            };
        }

        // Service availability check
        public bool IsFileShareServiceAvailable()
        {
            return _shareServiceClient != null;
        }
    }
}
