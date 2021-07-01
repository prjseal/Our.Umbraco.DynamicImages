# Our.Umbraco.DynamicImages

![Nuget](https://img.shields.io/nuget/v/Our.Umbraco.DynamicImages)
[![Nuget Downloads](https://img.shields.io/nuget/dt/Our.Umbraco.DynamicImages.svg)](https://www.nuget.org/packages/Our.Umbraco.DynamicImages)

This package allows you to create dynamic images for the main purpose of giving you better looking social share images, a bit like the ones on GitHub.

Using this package you can generate the image and return the bytes of the image which can then be used to save as a image file or return as a response. 

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

Soon I will add an example of how you can generate an image and get it to be created as a media item for you.