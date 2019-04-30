using Demo.Foundation.MediaLibrary.Data.DataProviders;
using Demo.Foundation.MediaLibrary.Interfaces;
using Sitecore.Resources.Media;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo.Foundation.MediaLibrary.Tests
{
    // Note: Configure the Azure Storage Emulator to execute below test cases
    // Reference: https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
    public class TestBase
    {
        protected readonly IBlobStorageProvider _blobStorageProvider;

        // Note: Make sure the blob container and image are exists
        protected const string _storageContainerName = "sitecore-media-library";
        protected const string _storageConnectionString = "UseDevelopmentStorage=true";
        protected const string _mediaFilePath = @"c:\temp\test.jpg";
        protected const string _mediaFileExtension = "jpg";
        protected List<string> _mediaList;

        public TestBase()
        {
            _blobStorageProvider = new AzureBlobStorageProvider(_storageContainerName, _storageConnectionString);
            _mediaList = new List<string>();
        }

        protected string GetMediaId()
        {
            var mediaId = _mediaList.Any() ? _mediaList.First() : Guid.NewGuid().ToString();
            if (!_mediaList.Contains(mediaId))
            {
                _mediaList.Add(mediaId);
            }

            return mediaId;
        }

        protected TransformationOptions GetTransformationOptions()
        {
            return new TransformationOptions()
            {
                AllowStretch = false,
                CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy,
                IgnoreAspectRatio = false,
                InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High,
                MaxSize = new System.Drawing.Size(0, 0),
                PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half,
                PreserveResolution = true,
                Quality = 95,
                Scale = 0,
                Size = new System.Drawing.Size(32, 32)
            };
        }

        protected ImageFormat GetImageFormat()
        {
            return MediaManager.Config.GetImageFormat(_mediaFileExtension);
        }
    }
}
