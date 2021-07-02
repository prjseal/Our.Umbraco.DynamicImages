# Our.Umbraco.DynamicImages

![Nuget](https://img.shields.io/nuget/v/Our.Umbraco.DynamicImages)
[![Nuget Downloads](https://img.shields.io/nuget/dt/Our.Umbraco.DynamicImages.svg)](https://www.nuget.org/packages/Our.Umbraco.DynamicImages)

This package allows you to create dynamic images for the main purpose of giving you better looking social share images, a bit like the ones on GitHub.

Using this package you can generate the image and return the bytes of the image which can then be used to save as a image file or return as a response. 

## Register the dynamic image service using a composer

```cs
using Our.Umbraco.DynamicImages.Services;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace CodeShare.Core.Composing
{
    public class RegisterServicesComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.Register<IDynamicImageService, DynamicImageService>(Lifetime.Singleton);
        }
    }
}
```

## Generate as media item on save

This library can generate the image and save it as an Umbraco media item for you.
Here is an example for you to use in your project and edit accordingly.

```cs
//using CodeShare.Core.Extensions;
using ImageProcessor.Imaging;
using Our.Umbraco.DynamicImages.Services;
using Our.Umbraco.DynamicImages.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using Umbraco.Web;

namespace CodeShare.Core.Composing
{
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    public class ContentSavingComposer : ComponentComposer<ContentSavingComponent>
    { }

    public class ContentSavingComponent : IComponent
    {
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly IDynamicImageService _dynamicImageService;
        private readonly ILogger _logger;

        public ContentSavingComponent(IUmbracoContextFactory umbracoContextFactory,
            IDynamicImageService dynamicImageService,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
            ILogger logger)
        {
            _umbracoContextFactory = umbracoContextFactory;
            _dynamicImageService = dynamicImageService;
            _logger = logger;
        }

        public void Initialize()
        {
            ContentService.Saving += ContentService_Saving;
        }


        private void ContentService_Saving(IContentService sender, ContentSavingEventArgs e)
        {
            try
            {
                var autoCreateImage = bool.Parse(ConfigurationManager.AppSettings["DynamicImageService:AutoCreateImage"]);
                var FontFamily = ConfigurationManager.AppSettings["DynamicImageService:FontFamily"];

                var parentFolderId = int.Parse(ConfigurationManager.AppSettings["DynamicImageService:ParentFolderId"]);

                using (UmbracoContextReference umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext())
                {
                    var domainAddress = umbracoContextReference.UmbracoContext.HttpContext.Request.Url.GetLeftPart(UriPartial.Authority);
                    var backgroundImageUrl = domainAddress + ConfigurationManager.AppSettings["DynamicImageService:BackgroundImage"];

                    foreach (var post in e.SavedEntities.Where(x => x.ContentType.Alias == "article"))
                    {
                        if (!autoCreateImage && !post.IsPropertyDirty("updateSocialImage")) break;

                        var articleTitle = post.Name;
                        var articleDate = post.GetValue<DateTime>("articleDate");

                        IPublishedContent author = GetAuthorContentItem(umbracoContextReference, post);

                        string authorName = string.Empty;
                        string authorImageUrl = string.Empty;
                        if (author != null)
                        {
                            authorName = author.Name;
                            authorImageUrl = GetAuthorImageUrl(domainAddress, authorImageUrl, author);
                        }

                        int detailsFontSize = 30;
                        int detailsYPosition = 530;

                        var textLayers = new List<TextLayer>();

                        //add author name
                        textLayers.Add(
                            _dynamicImageService.GetTextLayer(authorName, "c13ea9", detailsFontSize, 180, detailsYPosition, FontFamily, false)
                        );

                        //add article date
                        textLayers.Add(
                            _dynamicImageService.GetTextLayer(articleDate.ToString("dd MMMM yyyy"), "ffffff", detailsFontSize, 615, detailsYPosition, FontFamily, false)
                        );

                        //add issue number
                        textLayers.Add(
                            _dynamicImageService.GetTextLayer("73", "c13ea9", detailsFontSize, 530, detailsYPosition, FontFamily, false)
                        );

                        //get title layer settings
                        var titleTextLayerSettings = new TextLayerSettings()
                        {
                            Text = articleTitle.ToUpper(),
                            FontFamily = FontFamily,
                            FontSize = 90,
                            Colour = "ffffff",
                            DropShadow = false,
                            LineHeight = 110,
                            MaxLineLength = 20,
                            MaxLines = 3,
                            XPosition = 50,
                            YPosition = 50
                        };

                        //add title
                        textLayers.AddRange(
                            _dynamicImageService.GetTitleLayers(titleTextLayerSettings)
                        );

                        var imageLayers = new List<ImageLayer>();

                        //get author image layer settings
                        var authorImageSettings = new ImageLayerSettings()
                        {
                            Height = 100,
                            Width = 100,
                            XPosition = 50,
                            YPosition = 480,
                            IsCircle = true,
                            Quality = 70,
                            Url = authorImageUrl
                        };

                        //add author image
                        imageLayers.Add(
                            _dynamicImageService.GetImageLayer(authorImageSettings)
                        );

                        //create new dynamic image settings to hold the layers and background image settings
                        DynamicImageSettings dynamicImageSettings = new DynamicImageSettings()
                        {
                            BackgroundImageUrl = backgroundImageUrl,
                            BackgroundImageQuality = 70,
                            ImageLayers = imageLayers,
                            TextLayers = textLayers
                        };

                        //generate the image as an Umbraco Media Item
                        var udi = _dynamicImageService.GenerateImageAsMediaItem(dynamicImageSettings, parentFolderId, "media", articleTitle);

                        if (udi != null)
                        {
                            var udiString = udi.ToString();
                            post.SetValue("socialImage", udiString);
                            post.SetValue("updateSocialImage", false);
                            sender.Save(post, raiseEvents: false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(typeof(ContentSavingComponent), ex, "Error when trying to generate the image for this article");
                e.Messages.Add(new EventMessage("Image Generation", "Error when trying to generate the image for this article", EventMessageType.Warning));
            }
        }

        private static string GetAuthorImageUrl(string domainAddress, string authorImageUrl, IPublishedContent author)
        {
            var authorImage = author.Value<IPublishedContent>("mainImage");
            if (authorImage != null)
            {
                authorImageUrl = domainAddress + authorImage?.GetCropUrl(100, 100) ?? "";
            }

            return authorImageUrl;
        }

        private static IPublishedContent GetAuthorContentItem(UmbracoContextReference umbracoContextReference, Umbraco.Core.Models.IContent post)
        {
            IPublishedContent author = null;
            string authorId = post.GetValue<string>("author").Split(' ')[0];
            if (!string.IsNullOrWhiteSpace(authorId))
            {
                author = umbracoContextReference.UmbracoContext.Content.GetById(Udi.Parse(authorId));
            }

            return author;
        }

        public void Terminate()
        {
            // Nothing to terminate
        }
    }
}
```

## Generate a image file from a controller action

Here is an example controller which has a method to return an image as a file:

```cs
using ImageProcessor.Imaging;
using Our.Umbraco.DynamicImages.Services;
using Our.Umbraco.DynamicImages.Settings;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Mvc;

namespace CodeShare.Web.Controllers
{
    public class DynamicImageSurfaceController : SurfaceController
    {
        private IDynamicImageService _dynamicImageService { get; set; }

        public DynamicImageSurfaceController(IDynamicImageService dynamicImageService)
        {
            _dynamicImageService = dynamicImageService;
        }

        public FileContentResult GetSkriftImage()
        {
            // You could use real Umbraco content to set these values. I have hard coded for demo purposes
            var articleTitle = "Creating an author picker using contentment";
            var articleDate = new DateTime(2021, 06, 01);
            string authorName = "Paul Seal";
            string authorImageUrl = "https://codeshare.co.uk/media/mkzbdrvf/paul-seal-profile-2019-square.jpg?anchor=center&mode=crop&width=100&height=100";
            var FontFamily = "Microsoft Sans Serif";
            var backgroundImageUrl = "https://codeshare.co.uk/img/skrift-background.png";
            int detailsFontSize = 30;
            int detailsYPosition = 530;

            var textLayers = new List<TextLayer>();

            //add author name
            textLayers.Add(
                _dynamicImageService.GetTextLayer(authorName , "c13ea9", detailsFontSize, 180, detailsYPosition, FontFamily, false)
            );

            //add article date
            textLayers.Add(
                _dynamicImageService.GetTextLayer(articleDate.ToString("dd MMMM yyyy"), "ffffff", detailsFontSize, 615, detailsYPosition, FontFamily, false)
            );

            //add issue number
            textLayers.Add(
                _dynamicImageService.GetTextLayer("73", "c13ea9", detailsFontSize, 530, detailsYPosition, FontFamily, false)
            );

            //get title layer settings
            var titleTextLayerSettings = new TextLayerSettings()
            {
                Text = articleTitle.ToUpper(),
                FontFamily = FontFamily,
                FontSize = 90,
                Colour = "ffffff",
                DropShadow = false,
                LineHeight = 110,
                MaxLineLength = 20,
                MaxLines = 3,
                XPosition = 50,
                YPosition = 50
            };

            //add title
            textLayers.AddRange(
                _dynamicImageService.GetTitleLayers(titleTextLayerSettings)
            );

            var imageLayers = new List<ImageLayer>();
            
            //get author image layer settings
            var authorImageSettings = new ImageLayerSettings()
            {
                Height = 100,
                Width = 100,
                XPosition = 50,
                YPosition = 480,
                IsCircle = true,
                Quality = 70,
                Url = authorImageUrl
            };

            //add author image
            imageLayers.Add(
                _dynamicImageService.GetImageLayer(authorImageSettings)
            );

            //create new dynamic image settings to hold the layers and background image settings
            DynamicImageSettings dynamicImageSettings = new DynamicImageSettings()
            {
                BackgroundImageUrl = backgroundImageUrl,
                BackgroundImageQuality = 70,
                ImageLayers = imageLayers,
                TextLayers = textLayers
            };

            //generate the image as a byte array
            var imageBytes = _dynamicImageService.GenerateImageAsBytes(dynamicImageSettings);

            //return it as a file
            return File(imageBytes, "image/png", articleTitle.ToUrlSegment() + ".png");
        }
    }
}
```