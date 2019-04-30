using Sitecore.Resources.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Demo.Foundation.MediaLibrary.Tests
{
    // Test cases for Sitecore's Sitecore.Resources.Media.ImageEffectsResize.ResizeImageStream() method
    public class ImageEffectsResizeTests : TestBase
    {
        private readonly ImageEffectsResize _imageEffectsResize;

        public ImageEffectsResizeTests()
        {
            _imageEffectsResize = new ImageEffectsResize();
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
                var outputStream = _imageEffectsResize.ResizeImageStream(inputStream, transformationOptions, imageFormat);

                // Assert
                System.Diagnostics.Debug.Print($"Stream length {outputStream.Length}");
                Assert.True(outputStream.Length > 0);
            }
        }

        [Fact]
        public void Get_Stream_From_File_Then_Convert_It_To_MemoryStream_And_Then_Resize_It_Should_Throws_ArgumentException()
        {
            using (Stream inputStream = (Stream)File.OpenRead(_mediaFilePath))
            {
                // Arrange
                var transformationOptions = GetTransformationOptions();
                var imageFormat = GetImageFormat();

                // Act
                var memoryStream = new MemoryStream();
                inputStream.CopyTo(memoryStream);
                Action act = () => _imageEffectsResize.ResizeImageStream(memoryStream, transformationOptions, imageFormat);
                var exception = Record.Exception(act);

                // Assert
                Assert.NotNull(exception);
                Assert.IsType<ArgumentException>(exception);
            }
        }
    }
}
