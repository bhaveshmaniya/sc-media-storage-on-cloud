using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.ImageLib;
using Sitecore.Resources.Media;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace Demo.Foundation.MediaLibrary.Infrastructure.Pipelines.GetMediaStream
{
    /// <summary>
    /// Represents a ImageResizer -> Copied from the Sitecore.Resources.Media.ImageEffectsResize
    /// Question : Why we've added it?
    /// Answer   : Basically we are having an issue with the thumbnail generation, where we've the image stream stored in the MemoryStream.
    ///            If we can see the ResizeImageStream() method of the Sitecore.Resources.Media.ImageEffectsResize class, it takes the System.IO.Stream as the 
    ///            input Stream parameter (not System.IO.MemoryStream). Also inside that method Sitecore creates the System.IO.MemoryStream object and copies the
    ///            input stream inside it using the FileUtil.CopyStream() method, then it uses the System.IO.MemoryStream object for Bitmap object creation, which
    ///            is causing the issue and throws the exception "System.ArgumentException Message: Parameter is not valid. Source: System.Drawing at System.Drawing.Bitmap..ctor(Stream stream) at"
    ///            For more information refer: https://sitecore.stackexchange.com/questions/18123/error-could-not-run-the-getmediastream-pipeline-for-sitecore-media-library-i
    ///                      
    ///            - To fix the above issue we've created new method called ResizeImageFromStream() and did some modification 
    ///              (it actually copied from the Sitecore.Resources.Media.ImageEffectsResize.ResizeImageStream() method)
    /// </summary>
    public class ImageResizer
    {
        /// <summary>
        /// Resizes an image represented by a stream.
        /// </summary>
        /// <param name="inputStream">The input stream.</param>
        /// <param name="options">The options.</param>
        /// <param name="outputFormat">The output format.</param>
        /// <returns>The image stream.</returns>
        public virtual Stream ResizeImageFromStream(Stream inputStream, TransformationOptions options, ImageFormat outputFormat)
        {
            Assert.ArgumentNotNull((object)inputStream, "inputStream");
            Assert.ArgumentNotNull((object)options, "options");
            Assert.ArgumentNotNull((object)outputFormat, "outputFormat");
            ResizeOptions resizeOptions = this.GetResizeOptions(options);
            if (resizeOptions.IsEmpty)
                return inputStream;
            if (inputStream.Length > Settings.Media.MaxSizeInMemory)
            {
                Tracer.Error((object)"Could not resize image stream as it was larger than the maximum size allowed for memory processing.");
                return (Stream)null;
            }
            if (Settings.Media.UseLegacyResizing)
                return this.ResizeLegacy(inputStream, options, outputFormat);
            Resizer resizer = new Resizer();
            using (Bitmap originalBitmap = new Bitmap((Stream)inputStream))
            {
                Size frameSize = resizer.GetFrameSize(originalBitmap, resizeOptions);
                if (originalBitmap.Size.Equals((object)frameSize))
                {
                    inputStream.Seek(0L, SeekOrigin.Begin);
                    return (Stream)inputStream;
                }
                using (Bitmap resizedBitmap = resizer.Resize(originalBitmap, resizeOptions, outputFormat))
                {
                    MemoryStream memoryStream = new MemoryStream();
                    memoryStream.Seek(0L, SeekOrigin.Begin);
                    ImageCodecInfo encoderInfo = this.FindEncoderInfo(outputFormat);
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)options.Quality);
                    resizedBitmap.Save((Stream)memoryStream, encoderInfo, encoderParams);
                    memoryStream.SetLength(memoryStream.Position);
                    memoryStream.Seek(0L, SeekOrigin.Begin);
                    return (Stream)memoryStream;
                }
            }
        }

        /// <summary>
        /// Gets the resize options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>The resize options.</returns>
        protected ResizeOptions GetResizeOptions(TransformationOptions options)
        {
            return new ResizeOptions
            {
                AllowStretch = options.AllowStretch,
                BackgroundColor = options.BackgroundColor,
                IgnoreAspectRatio = options.IgnoreAspectRatio,
                MaxSize = options.MaxSize,
                Scale = options.Scale,
                Size = options.Size,
                PreserveResolution = options.PreserveResolution,
                CompositingMode = options.CompositingMode,
                InterpolationMode = options.InterpolationMode,
                PixelOffsetMode = options.PixelOffsetMode
            };
        }

        /// <summary>
        /// Gets the scale.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="originalImage">The original image.</param>
        /// <param name="size">The size.</param>
        /// <returns>The scale.</returns>
        protected float GetScale(TransformationOptions options, Image originalImage, Size size)
        {
            float val = (float)size.Width / (float)originalImage.Width;
            float val2 = (float)size.Height / (float)originalImage.Height;
            float num = Math.Min(val, val2);
            if (!options.AllowStretch && num > 1f)
            {
                num = 1f;
            }
            return num;
        }

        /// <summary>
        /// Gets the size.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="size">The size.</param>
        /// <returns>The transformed size.</returns>
        protected Size GetSize(TransformationOptions options, Size size)
        {
            if (options.MaxSize.IsEmpty)
            {
                return size;
            }
            if (options.MaxSize.Width > 0 && size.Width > options.MaxSize.Width)
            {
                if (options.Size.Height == 0)
                {
                    size.Height = (int)Math.Round((double)((float)options.MaxSize.Width / (float)size.Width * (float)size.Height));
                }
                size.Width = options.MaxSize.Width;
            }
            if (options.MaxSize.Height > 0 && size.Height > options.MaxSize.Height)
            {
                if (options.Size.Width == 0)
                {
                    size.Width = (int)Math.Round((double)((float)options.MaxSize.Height / (float)size.Height * (float)size.Width));
                }
                size.Height = options.MaxSize.Height;
            }
            return size;
        }

        /// <summary>
        /// Finds the encoder info for image format.
        /// </summary>
        /// <param name="outputFormat">The output format.</param>
        /// <returns>The encoder info.</returns>
        /// <exception cref="T:System.InvalidOperationException">Unknown image format</exception>
        protected ImageCodecInfo FindEncoderInfo(ImageFormat outputFormat)
        {
            ImageCodecInfo[] imageEncoders = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < imageEncoders.Length; i++)
            {
                ImageCodecInfo imageCodecInfo = imageEncoders[i];
                if (imageCodecInfo.FormatID.Equals(outputFormat.Guid))
                {
                    return imageCodecInfo;
                }
            }
            throw new InvalidOperationException("Unknown image format");
        }

        /// <summary>
        /// Gets the set of options to use in the image transformation.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="image">The image.</param>
        /// <returns>The transformed size.</returns>
        protected Size GetSize(TransformationOptions options, Image image)
        {
            if (options.Scale > 0f)
            {
                return new Size(this.Scale(image.Width, options.Scale), this.Scale(image.Height, options.Scale));
            }
            if (options.Size.IsEmpty || options.Size == image.Size)
            {
                return new Size(image.Size.Width, image.Size.Height);
            }
            if (options.Size.Width == 0)
            {
                float amount = (float)options.Size.Height / (float)image.Height;
                return new Size(this.Scale(image.Width, amount), options.Size.Height);
            }
            if (options.Size.Height == 0)
            {
                float amount2 = (float)options.Size.Width / (float)image.Width;
                return new Size(options.Size.Width, this.Scale(image.Height, amount2));
            }
            return new Size(options.Size.Width, options.Size.Height);
        }

        /// <summary>
        /// Scales the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="amount">The scale amount.</param>
        /// <returns>The scaled value.</returns>
        protected int Scale(int value, float amount)
        {
            return (int)Math.Round((double)((float)value * amount));
        }

        /// <summary>
        /// Resizes an image represented by a stream.
        /// </summary>
        /// <param name="inputStream">The input stream.</param>
        /// <param name="options">The options.</param>
        /// <param name="outputFormat">The output format.</param>
        /// <returns>The image stream.</returns>
        protected Stream ResizeLegacy(Stream inputStream, TransformationOptions options, ImageFormat outputFormat)
        {
            Assert.ArgumentNotNull(inputStream, "inputStream");
            Assert.ArgumentNotNull(options, "options");
            Image image = Image.FromStream(inputStream);
            MemoryStream memoryStream = new MemoryStream();
            Size size = this.GetSize(options, image);
            size = this.GetSize(options, size);
            if (size != image.Size)
            {
                float scale = this.GetScale(options, image, size);
                int num = this.Scale(image.Width, scale);
                int num2 = this.Scale(image.Height, scale);
                Rectangle rect = new Rectangle((size.Width - num) / 2, (size.Height - num2) / 2, num, num2);
                Bitmap bitmap = new Bitmap(image, size);
                bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = options.InterpolationMode;
                    graphics.Clear(options.BackgroundColor);
                    graphics.DrawImage(image, rect);
                }
                bitmap.Save(memoryStream, outputFormat);
            }
            else
            {
                image.Save(memoryStream, outputFormat);
            }
            if (memoryStream.CanSeek)
            {
                memoryStream.Seek(0L, SeekOrigin.Begin);
            }
            return memoryStream;
        }
    }
}