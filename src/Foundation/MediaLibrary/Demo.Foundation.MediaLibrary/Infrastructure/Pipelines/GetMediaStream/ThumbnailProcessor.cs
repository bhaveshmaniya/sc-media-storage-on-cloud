using Sitecore.Diagnostics;
using Sitecore.Resources.Media;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Web;

namespace Demo.Foundation.MediaLibrary.Infrastructure.Pipelines.GetMediaStream
{
    public class ThumbnailProcessor
    {
        public void Process(GetMediaStreamPipelineArgs args)
        {
            try
            {
                if (!args.Options.Thumbnail)
                    return;

                TransformationOptions transformationOptions = args.Options.GetTransformationOptions();
                ImageFormat imageFormat = MediaManager.Config.GetImageFormat(args.MediaData.MediaItem.Extension);

                var imageResizer = new ImageResizer();
                var stream = imageResizer.ResizeImageFromStream(args.MediaData.GetStream().Stream, transformationOptions, imageFormat);
                if (stream != null)
                {
                    args.OutputStream = new MediaStream(stream, args.MediaData.MediaItem.Extension, args.MediaData.MediaItem);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error while generating thumbnail for media item: {args.MediaData.MediaId}", ex, this);
            }
        }
    }
}