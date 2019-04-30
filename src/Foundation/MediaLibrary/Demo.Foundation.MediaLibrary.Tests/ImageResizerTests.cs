using Demo.Foundation.MediaLibrary.Data.DataProviders;
using Demo.Foundation.MediaLibrary.Infrastructure.Pipelines.GetMediaStream;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Demo.Foundation.MediaLibrary.Tests
{
    // Test cases for custom ImageResizer.ResizeImageFromStream() method 
    // - This is to fix the image resizing issue of Sitecore.Resources.Media.ImageEffectsResize.ResizeImageStream() method while passing the System.IO.MemoryStream
    public class ImageResizerTests : TestBase
    {
        private readonly ImageResizer _imageResizer;
        //private const string _downloadMediaFromBlobFilePath = @"c:\temp\test_from_blob.jpg";

        public ImageResizerTests()
        {
            _imageResizer = new ImageResizer();
        }

        [Fact]
        public void Get_Stream_From_File_And_Then_Resize_It_Should_Success()
        {
            using (Stream inputStream = (Stream)File.OpenRead(_mediaFilePath))
            {
                // Arrange
                var transformationOptions = GetTransformationOptions();
                var imageFormat = GetImageFormat();

                // Act
                var outputStream = _imageResizer.ResizeImageFromStream(inputStream, transformationOptions, imageFormat);

                // Assert
                System.Diagnostics.Debug.Print($"Stream length {outputStream.Length}");
                Assert.True(outputStream.Length > 0);
            }
        }

        [Fact]
        public void Get_Stream_From_File_Then_Convert_It_To_MemoryStream_And_Then_Resize_It_Should_Success()
        {
            using (Stream inputStream = (Stream)File.OpenRead(_mediaFilePath))
            {
                // Arrange
                var transformationOptions = GetTransformationOptions();
                var imageFormat = GetImageFormat();

                // Act
                var memoryStream = new MemoryStream();
                inputStream.CopyTo(memoryStream);

                var outputStream = _imageResizer.ResizeImageFromStream(memoryStream, transformationOptions, imageFormat);

                // Assert
                System.Diagnostics.Debug.Print($"Stream length {outputStream.Length}");
                Assert.True(outputStream.Length > 0);
            }
        }

        //[Fact]
        //public void AzureBlobStorage_Put_Media_Then_Download_To_File_Then_Resize_It_And_Then_Delete_Media_Should_Success()
        //{
        //    // 1. Put Media
        //    // Arrange
        //    var blobStorageProvider = new AzureBlobStorageProvider(_storageContainerName, _storageConnectionString);
        //    var fileStream = File.OpenRead(_mediaFilePath);
        //    var mediaId = GetMediaId();
        //    _mediaList.Add(mediaId);

        //    // Act
        //    blobStorageProvider.Put(fileStream, mediaId);

        //    // Assert
        //    Assert.Equal(mediaId, blobStorageProvider.GetBlobReference(mediaId)?.Name);

        //    // 2. Download Blob to File
        //    // Act
        //    blobStorageProvider.DownloadToFile(mediaId, _downloadMediaFromBlobFilePath);

        //    // Assert
        //    Assert.True(File.Exists(_downloadMediaFromBlobFilePath));

        //    // 3. Resize It
        //    using (Stream inputStream = (Stream)File.OpenRead(_downloadMediaFromBlobFilePath))
        //    {
        //        // Arrange
        //        var transformationOptions = GetTransformationOptions();
        //        var imageFormat = GetImageFormat();

        //        // Act
        //        var outputStream = _imageResizer.ResizeImageFromStream(inputStream, transformationOptions, imageFormat);

        //        // Assert
        //        System.Diagnostics.Debug.Print($"Stream length {outputStream.Length}");
        //        Assert.True(outputStream.Length > 0);
        //    }

        //    // 4. Delete Media
        //    // Act
        //    var success = blobStorageProvider.Delete(mediaId);

        //    // Assert
        //    Assert.True(success);
        //}

        //[Fact]
        //public void AzureBlobStorage_Put_Media_Then_Get_MemoryStream_Then_Resize_It_And_Then_Delete_Media_Should_Success()
        //{
        //    // 1. Put Media
        //    // Arrange
        //    var blobStorageProvider = new AzureBlobStorageProvider(_storageContainerName, _storageConnectionString);
        //    var fileStream = File.OpenRead(_mediaFilePath);
        //    var mediaId = GetMediaId();
        //    _mediaList.Add(mediaId);

        //    // Act
        //    blobStorageProvider.Put(fileStream, mediaId);

        //    // Assert
        //    Assert.Equal(mediaId, blobStorageProvider.GetBlobReference(mediaId)?.Name);

        //    // 2. Get Stream
        //    // Act
        //    var stream = new MemoryStream();
        //    blobStorageProvider.Get(stream, mediaId);

        //    // Assert
        //    System.Diagnostics.Debug.Print($"Stream length {stream.Length}");
        //    Assert.True(stream.Length > 0);

        //    // 3.Resize Image
        //    // Arrange
        //    var transformationOptions = GetTransformationOptions();
        //    var imageFormat = GetImageFormat();

        //    // Act
        //    var outputStream = _imageResizer.ResizeImageFromStream(stream, transformationOptions, imageFormat);

        //    // Assert
        //    System.Diagnostics.Debug.Print($"Stream length {outputStream.Length}");
        //    Assert.True(outputStream.Length > 0);

        //    // 4. Delete Media
        //    // Act
        //    var success = blobStorageProvider.Delete(mediaId);

        //    // Assert
        //    Assert.True(success);
        //}
    }
}
