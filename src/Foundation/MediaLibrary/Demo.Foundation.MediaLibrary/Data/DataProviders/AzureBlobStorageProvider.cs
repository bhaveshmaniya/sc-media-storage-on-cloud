using Demo.Foundation.MediaLibrary.Helpers;
using Demo.Foundation.MediaLibrary.Interfaces;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Demo.Foundation.MediaLibrary.Data.DataProviders
{
    public class AzureBlobStorageProvider : IBlobStorageProvider
    {
        private readonly CloudBlobContainer _blobContainer;

        private static readonly string _storageContainerName = Configuration.Settings.Media.AzureBlobStorage.StorageContainerName;
        private static readonly string _storageConnectionString = Configuration.Settings.Media.AzureBlobStorage.StorageConnectionString;

        public string StorageContainerName { get; private set; }
        public string StorageConnectionString { get; private set; }

        public AzureBlobStorageProvider(string storageContainerName, string storageConnectionString)
        {
            this.StorageContainerName = storageContainerName;
            this.StorageConnectionString = storageConnectionString;

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(this.StorageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            this._blobContainer = blobClient.GetContainerReference(this.StorageContainerName);
        }

        public AzureBlobStorageProvider()
            : this(_storageContainerName, _storageConnectionString)
        { }

        public bool Delete(string blobId)
        {
            var blob = this._blobContainer.GetBlobReference(blobId);

            var requestProperties = GetRequestProperties();
            var success = blob.DeleteIfExists(options: requestProperties.Item1, operationContext: requestProperties.Item2);

            LogHelper.LogInfo($"Blob with Id {blobId} {(success ? "was" : "was not")} deleted");

            if (!success)
            {
                LogHelper.LogInfo($"Delete operation context StatusCode: {requestProperties.Item2.LastResult?.HttpStatusCode}, StatusMessage: {requestProperties.Item2.LastResult?.HttpStatusMessage}, ErrorMessage: {requestProperties.Item2.LastResult?.ExtendedErrorInformation?.ErrorMessage}");
            }

            return success;
        }

        public bool Exists(string blobId)
        {
            var blob = this._blobContainer.GetBlobReference(blobId);

            return blob.Exists();
        }

        public void Get(Stream target, string blobId)
        {
            var blob = this._blobContainer.GetBlockBlobReference(blobId);

            if (!blob.Exists())
                return;

            var requestProperties = GetRequestProperties();
            var timer = new HighResTimer(true);
            blob.DownloadToStream(target: target, options: requestProperties.Item1, operationContext: requestProperties.Item2);
            timer.Stop();

            LogHelper.LogInfo($"Download of blob with Id {blobId} of size {target.Length} took {timer.ElapsedTimeSpan.ToString(Constants.TimeSpanDebugFormat)}");
        }

        public void Put(Stream stream, string blobId)
        {
            // Use BlockBlob to store media assets
            // If blob with such Id already exists, it will be overwritten
            var blob = this._blobContainer.GetBlockBlobReference(blobId);

            // Set blob streaming chunks if file larger than value specified in SingleBlobUploadThresholdInBytes setting
            var streamWriteSizeBytes = Configuration.Settings.Media.AzureBlobStorage.StreamWriteSizeInBytes;
            if (streamWriteSizeBytes.HasValue && streamWriteSizeBytes.Value > 0)
            {
                blob.StreamWriteSizeInBytes = streamWriteSizeBytes.Value;
            }

            var requestProperties = GetRequestProperties();
            var timer = new HighResTimer(true);
            blob.UploadFromStream(stream, options: requestProperties.Item1, operationContext: requestProperties.Item2);
            timer.Stop();

            LogHelper.LogInfo($"Upload of blob {blobId} of size {stream.Length} took {timer.ElapsedTimeSpan.ToString(Constants.TimeSpanDebugFormat)}");
#if DEBUG
            var requestsInfo = new StringBuilder();
            foreach (var request in requestProperties.Item2.RequestResults)
            {
                requestsInfo.AppendLine($"{request.ServiceRequestID} - {request.HttpStatusCode} - {request.HttpStatusMessage} - bytes: {request.EgressBytes}");
            }
            Log.Info($"operationContext.RequestResults: {requestsInfo}", this);
#endif
        }

        public void DownloadToFile(string blobId, string filePath)
        {
            var blob = this._blobContainer.GetBlockBlobReference(blobId);

            if (!blob.Exists())
                return;

            blob.DownloadToFile(filePath, FileMode.Create);
        }

        public virtual CloudBlob GetBlobReference(string blobId)
        {
            return this._blobContainer.GetBlobReference(blobId);
        }

        protected virtual System.Tuple<BlobRequestOptions, OperationContext> GetRequestProperties()
        {
            var requestOptions = new BlobRequestOptions()
            {
                MaximumExecutionTime = Configuration.Settings.Media.AzureBlobStorage.MaximumExecutionTime,
                ParallelOperationThreadCount = Configuration.Settings.Media.AzureBlobStorage.ParallelOperationThreadCount,
                RetryPolicy = Configuration.Settings.Media.AzureBlobStorage.RetryPolicy,
                ServerTimeout = Configuration.Settings.Media.AzureBlobStorage.RequestServerTimeout,
                SingleBlobUploadThresholdInBytes = Configuration.Settings.Media.AzureBlobStorage.SingleBlobUploadThresholdInBytes,
                StoreBlobContentMD5 = Configuration.Settings.Media.AzureBlobStorage.StoreBlobContentMD5,
                UseTransactionalMD5 = Configuration.Settings.Media.AzureBlobStorage.UseTransactionalMD5
            };

            var operationContext = new OperationContext()
            {
                LogLevel = Configuration.Settings.Media.AzureBlobStorage.OperationContextLogLevel
            };

            return System.Tuple.Create(requestOptions, operationContext);
        }
    }
}