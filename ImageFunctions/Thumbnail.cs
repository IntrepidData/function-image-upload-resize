// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

// Learn how to locally debug an Event Grid-triggered function:
//    https://aka.ms/AA30pjh

// Use for local testing:
//   https://{ID}.ngrok.io/runtime/webhooks/EventGrid?functionName=Thumbnail

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImageFunctions
{
    public static class Thumbnail
    {
        private static readonly string BLOB_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var uri = new Uri(bloblUrl);
            var blobClient = new BlobClient(uri);
            return blobClient.Name;
        }

        private static string GetContentType(string extension)
        {
            var contentType = "";

            extension = extension.Replace(".", "");

            if (extension == "" || extension.Length > 10 || extension == null)
            {
                extension = "PNG";
            }

            //var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            //if (isSupported)
            {
                switch (extension.ToLower())
                {
                    case "png":
                        contentType = "image/png";
                        break;
                    case "jpg":
                        contentType = "image/jpeg";
                        break;
                    case "jpeg":
                        contentType = "image/jpeg";
                        break;
                    case "gif":
                        contentType = "image/gif";
                        break;
                    default:
                        contentType = "application/octet-stream";
                        break;
                }
            }

            return contentType;
        }


        private static IImageEncoder GetEncoder(string extension)
        {
            IImageEncoder encoder = null;
                        
            extension = extension.Replace(".", "");

            if (extension == "" || extension.Length > 10 || extension == null)
            {
                extension = "PNG";
            }

            var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            if (isSupported)
            {
                switch (extension.ToLower())
                {
                    case "png":
                        encoder = new PngEncoder();
                        break;
                    case "jpg":
                        encoder = new JpegEncoder();
                        break;
                    case "jpeg":
                        encoder = new JpegEncoder();
                        break;
                    case "gif":
                        encoder = new GifEncoder();
                        break;
                    default:
                        break;
                }
            }

            return encoder;
        }

        [FunctionName("Thumbnail")]
        public static async Task Run(
            [EventGridTrigger]EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read)] Stream input,
            ILogger log)
        {
            try
            {
                if (input != null)
                {
                    var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
                    var extension = Path.GetExtension(createdEvent.Url);
                    var encoder = GetEncoder(extension);
                    log.LogInformation($"encoder: {encoder}");

                    if (encoder != null)
                    {
                        var thumbnailWidth = 100; //Convert.ToInt32(Environment.GetEnvironmentVariable("THUMBNAIL_WIDTH"));
                        var blobContainerName = Environment.GetEnvironmentVariable("THUMBNAIL_CONTAINER_NAME");
                        var connectionString = BLOB_STORAGE_CONNECTION_STRING;
                        //var blobServiceClient = new BlobServiceClient(BLOB_STORAGE_CONNECTION_STRING);
                        //var blobContainerClient = blobServiceClient.GetBlobContainerClient(thumbContainerName);
                        var blobName = GetBlobNameFromUrl(createdEvent.Url);
                        var blobClient = new BlobClient(connectionString, blobContainerName, blobName);
                        var contentType = GetContentType(extension);
                        var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };

                        using (var output = new MemoryStream())
                        using (Image<Rgba32> image = Image.Load(input))
                        {
                            log.LogInformation($"image.Width: {image.Width}");
                            log.LogInformation($"image.Height: {image.Height}");
                            log.LogInformation($"thumbnailWidth: {thumbnailWidth}");

                            var divisor = image.Width / thumbnailWidth;
                            
                            log.LogInformation($"divisor: {divisor}");

                            if (divisor < 1)
                            { 
                                divisor = 1;
                            }
                            
                            var height = Convert.ToInt32(Math.Round((decimal)(image.Height / divisor)));

                            if (divisor >= 1)
                            {
                                image.Mutate(x => x.Resize(thumbnailWidth, height));
                            }

                            image.Save(output, encoder);
                            output.Position = 0;
                            //await blobContainerClient.UploadBlobAsync(blobName, output);

                            await blobClient.UploadAsync(output, blobHttpHeaders);
                        }

                        //log.LogInformation($"blobName: {blobName}");
                        //log.LogInformation($"createdEvent: {createdEvent.Url}");
                        log.LogInformation($"contentType: {contentType}");
                        log.LogInformation($"blobContainerClient: {blobName}");

                    }
                    else
                    {
                        log.LogInformation($"No encoder support for: {createdEvent.Url}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                throw;
            }
        }
    }
}
