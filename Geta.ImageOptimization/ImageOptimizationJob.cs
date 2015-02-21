using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using EPiServer;
using EPiServer.BaseLibrary.Scheduling;
using EPiServer.Core;
using EPiServer.Data;
using EPiServer.DataAccess;
using EPiServer.Framework.Blobs;
using EPiServer.PlugIn;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Geta.ImageOptimization.Configuration;
using Geta.ImageOptimization.Implementations;
using Geta.ImageOptimization.Interfaces;
using Geta.ImageOptimization.Messaging;
using Geta.ImageOptimization.Models;

namespace Geta.ImageOptimization
{
    [ScheduledPlugIn(DisplayName = "Geta Image Optimization")]
    public class ImageOptimizationJob : JobBase
    {
        private bool _stop;
        private readonly IImageOptimization _imageOptimization;
        private readonly IImageLogRepository _imageLogRepository;

        public ImageOptimizationJob()
            : this(new Implementations.ImageOptimization(), new ImageLogRepository())
        {
            IsStoppable = true;
        }

        public ImageOptimizationJob(IImageOptimization imageOptimization, IImageLogRepository imageLogRepository)
        {
            this._imageOptimization = imageOptimization;
            this._imageLogRepository = imageLogRepository;
        }

        public override string Execute()
        {
            int count = 0;
            long totalBytesBefore = 0;
            long totalBytesAfter = 0;

            var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();
            var blobFactory = ServiceLocator.Current.GetInstance<BlobFactory>();

            var allImages = GetImageFiles(contentRepository.Get<ContentFolder>(SiteDefinition.Current.GlobalAssetsRoot));

            if (_stop)
            {
                return string.Format("Job stopped after optimizing {0} images.", count);
            }

            // TODO remove previously optimized/checked images

            foreach (ImageData image in allImages)
            {
                if (_stop)
                {
                    return string.Format("Job completed after optimizing: {0} images. Before: {1} KB, after: {2} KB.", count, totalBytesBefore / 1024, totalBytesAfter / 1024);
                }

                // TODO Check that image is public and published

                var imageOptimizationRequest = new ImageOptimizationRequest
                                                   {
                                                       ImageUrl = GetFriendlyUrl(image.ContentLink)
                                                   };

                ImageOptimizationResponse imageOptimizationResponse = this._imageOptimization.ProcessImage(imageOptimizationRequest);

                Identity logEntryId = this.AddLogEntry(imageOptimizationResponse, image);

                if (imageOptimizationResponse.Successful)
                {
                    totalBytesBefore += imageOptimizationResponse.OriginalImageSize;

                    if (imageOptimizationResponse.OptimizedImageSize > 0)
                    {
                        totalBytesAfter += imageOptimizationResponse.OptimizedImageSize;
                    }
                    else
                    {
                        totalBytesAfter += imageOptimizationResponse.OriginalImageSize;
                    }

                    var file = image.CreateWritableClone() as ImageData;

                    byte[] fileContent = imageOptimizationResponse.OptimizedImage;


                    var blob = blobFactory.CreateBlob(file.BinaryDataContainer, file.MimeType);

                    blob.Write(new MemoryStream(fileContent));

                    file.BinaryData = blob;

                    contentRepository.Save(file, SaveAction.Publish, AccessLevel.NoAccess);

                    //versioningFile.CheckIn(string.Format("Optimized image with Smush.it. From: {0} KB to: {1} KB. Saved: {2}%", imageOptimizationResponse.OriginalImageSize / 1024, imageOptimizationResponse.OptimizedImageSize / 1024, imageOptimizationResponse.PercentSaved));

                    this.UpdateLogEntryToOptimized(logEntryId);

                    count++;
                }
            }

            return string.Format("Job completed after optimizing: {0} images. Before: {1} KB, after: {2} KB.", count, totalBytesBefore / 1024, totalBytesAfter / 1024);
        }

        private IEnumerable<ImageData> GetImageFiles(ContentFolder contentFolder)
        {
            var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();

            var queue = new Queue<ContentFolder>();
            queue.Enqueue(contentFolder);
            while (queue.Count > 0)
            {
                contentFolder = queue.Dequeue();
                try
                {
                    foreach (ContentFolder subDir in contentLoader.GetChildren<ContentFolder>(contentFolder.ContentLink))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                catch
                {
                }
                IEnumerable<ImageData> files = null;
                try
                {
                    files = contentLoader.GetChildren<ImageData>(contentFolder.ContentLink);
                }
                catch
                {
                }
                if (files != null)
                {
                    foreach (var imageData in files)
                    {
                        yield return imageData;
                    }
                }
            }
        }

        public string GetFriendlyUrl(ContentReference contentReference)
        {
            if (ContentReference.IsNullOrEmpty(contentReference))
            {
                return string.Empty;
            }

            var urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();

            var url = urlResolver.GetUrl(contentReference);

            return GetExternalUrl(url);
        }

        public static string GetExternalUrl(string input)
        {
            var siteUri = HttpContext.Current != null
                            ? HttpContext.Current.Request.Url
                            : SiteDefinition.Current.SiteUrl;

            if (!string.IsNullOrEmpty(ImageOptimizationSettings.Settings.SiteUrl))
            {
                siteUri = new Uri(ImageOptimizationSettings.Settings.SiteUrl);
            }

            var urlBuilder = new UrlBuilder(input)
            {
                Scheme = siteUri.Scheme,
                Host = siteUri.Host,
                Port = siteUri.Port
            };

            return urlBuilder.ToString();
        }

        private void UpdateLogEntryToOptimized(Identity logEntryId)
        {
            ImageLogEntry logEntry = this._imageLogRepository.GetLogEntry(logEntryId);

            logEntry.IsOptimized = true;

            this._imageLogRepository.Save(logEntry);
        }

        private Identity AddLogEntry(ImageOptimizationResponse imageOptimizationResponse, ImageData imageData)
        {
            ImageLogEntry logEntry = this._imageLogRepository.GetLogEntry(imageOptimizationResponse.OriginalImageUrl) ?? new ImageLogEntry();

            logEntry.ContentReference = imageData.ContentLink;
            logEntry.OriginalSize = imageOptimizationResponse.OriginalImageSize;
            logEntry.OptimizedSize = imageOptimizationResponse.OptimizedImageSize;
            logEntry.PercentSaved = imageOptimizationResponse.PercentSaved;
            logEntry.ImageUrl = imageOptimizationResponse.OriginalImageUrl;

            return this._imageLogRepository.Save(logEntry);
        }

        public override void Stop()
        {
            _stop = true;
        }
    }
}