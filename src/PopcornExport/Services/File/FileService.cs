﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using PopcornExport.Extensions;
using PopcornExport.Helpers;
using PopcornExport.Models.Export;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace PopcornExport.Services.File
{
    /// <summary>
    /// File service
    /// </summary>
    public sealed class FileService : IFileService
    {
        /// <summary>
        /// True if service is initialized
        /// </summary>
        private bool _initialized;

        /// <summary>
        /// <see cref="CloudStorageAccount"/>
        /// </summary>
        private readonly CloudStorageAccount _storageAccount;

        /// <summary>
        /// Container
        /// </summary>
        private CloudBlobContainer _container;

        /// <summary>
        /// Create an instance of <see cref="FileService"/>
        /// </summary>
        /// <param name="accountName"></param>
        /// <param name="storageConnectionString"></param>
        public FileService(string accountName, string storageConnectionString)
        {
            _storageAccount = new CloudStorageAccount(new StorageCredentials(accountName, storageConnectionString),
                true);
        }

        /// <summary>
        /// Initialize the file service
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            var blobClient = _storageAccount.CreateCloudBlobClient();
            _container = blobClient.GetContainerReference(Constants.AssetsFolder);

            await _container.CreateIfNotExistsAsync();

            var permissions = await _container.GetPermissionsAsync();
            if (permissions.PublicAccess != BlobContainerPublicAccessType.Blob)
            {
                permissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                await _container.SetPermissionsAsync(permissions);
            }

            _initialized = true;
        }

        /// <summary>
        /// Upload a file to Azure Storage from a url
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="url">Url of the file</param>
        /// <param name="type"><see cref="ExportType"/></param>
        /// <param name="forceReplace">Force replacing an existing file</param>
        /// <returns>Uri of the uploaded file</returns>
        public async Task<string> UploadFileFromUrlToAzureStorage(string fileName, string url, ExportType type,
            bool forceReplace = false)
        {
            try
            {
                if (!_initialized) throw new Exception("Service is not initialized");

                var blob = _container.GetBlockBlobReference($@"{type.ToFriendlyString()}/{fileName}");
                if (forceReplace || !await blob.ExistsAsync())
                {
                    var cookieContainer = new CookieContainer();
                    using (var handler = new HttpClientHandler {CookieContainer = cookieContainer})
                    using (var client = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(10)
                    })
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        if (url.Contains("yts"))
                        {
                            // Sometimes YTS requires an authentication to access movies. Here is an already authenticated cookie valid until December 2017
                            cookieContainer.Add(new Uri(url),
                                new Cookie("PHPSESSID", "ncrmefe4s79la9d2mba0n538o6", "/", "yts.ag"));
                            cookieContainer.Add(new Uri(url),
                                new Cookie("__cfduid", "d6c81b283d74b436fec66f02bcb99c04d1481020053", "/", ".yts.ag"));
                            cookieContainer.Add(new Uri(url), new Cookie("bgb2", "1", "/", "yts.ag"));
                            cookieContainer.Add(new Uri(url),
                                new Cookie("uhh", "60a8c98fd72e7a79b731f1ea09a5d09d", "/", ".yts.ag"));
                            cookieContainer.Add(new Uri(url), new Cookie("uid", "2188423", "/", ".yts.ag"));
                        }
                        using (var response = await client.SendAsync(request))
                        {
                            response.EnsureSuccessStatusCode();
                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            {
                                var file = _container.GetBlockBlobReference($@"{type.ToFriendlyString()}/{fileName}");
                                if (blob.Name.Contains("background") ||
                                    blob.Name.Contains("banner") ||
                                    blob.Name.Contains("poster"))
                                {
                                    try
                                    {
                                        using (var stream = new MemoryStream())
                                        using (var image = Image.Load(contentStream, new JpegDecoder()))
                                        {
                                            if (blob.Name.Contains("background") || blob.Name.Contains("banner"))
                                                image.Mutate(x => x
                                                    .Resize(new ResizeOptions
                                                    {
                                                        Mode = ResizeMode.Stretch,
                                                        Size = new Size(1280, 720),
                                                        Sampler = new NearestNeighborResampler()
                                                    }));

                                            if (blob.Name.Contains("poster"))
                                                image.Mutate(x => x
                                                    .Resize(new ResizeOptions
                                                    {
                                                        Mode = ResizeMode.Stretch,
                                                        Size = new Size(400, 600),
                                                        Sampler = new NearestNeighborResampler()
                                                    }));

                                            image.SaveAsJpeg(stream, new JpegEncoder
                                            {
                                                Quality = 80
                                            });
                                            stream.Seek(0, SeekOrigin.Begin);

                                            await file.UploadFromStreamAsync(stream);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        await file.UploadFromStreamAsync(contentStream);
                                    }
                                }
                                else
                                {
                                    await file.UploadFromStreamAsync(contentStream);
                                }

                                return file.Uri.AbsoluteUri;
                            }
                        }
                    }
                }

                return blob.Uri.AbsoluteUri;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}