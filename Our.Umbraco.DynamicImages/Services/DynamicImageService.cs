using ImageProcessor;
using ImageProcessor.Imaging;
using ImageProcessor.Imaging.Formats;
using Our.Umbraco.DynamicImages.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using Umbraco.Core;
using Umbraco.Core.Services;

namespace Our.Umbraco.DynamicImages.Services
{
    public class DynamicImageService : IDynamicImageService
    {
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
        private readonly IMediaService _mediaService;

        public DynamicImageService(IContentTypeBaseServiceProvider contentTypeBaseServiceProvider, IMediaService mediaService)
        {
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
            _mediaService = mediaService;
        }

        public TextLayer GetTextLayer(TextLayerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Text)) return null;

            var textLayer = new TextLayer()
            {
                Text = settings.Text,
                FontColor = ColorTranslator.FromHtml("#" + settings.Colour),
                FontSize = settings.FontSize,
                Position = new Point(settings.XPosition, settings.YPosition),
                FontFamily = new FontFamily(settings.FontFamily),
                DropShadow = settings.DropShadow
            };
            return textLayer;
        }

        public ImageLayer GetImageLayer(ImageLayerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Url)) return null;

            var imageLayer = new ImageLayer();

            int rounding = 0;
            if (settings.IsCircle)
            {
                rounding = settings.Height / 2;
            }

            byte[] photoBytes = GetImageBytesFromUrl(settings.Url);
            ISupportedImageFormat format = new PngFormat() { Quality = settings.Quality };
            using (MemoryStream inStream = new MemoryStream(photoBytes))
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    using (ImageFactory imageFactory = new ImageFactory(preserveExifData: true))
                    {
                        imageFactory.Load(inStream)
                            .Format(format)
                            .Resize(new Size(settings.Width, settings.Height));

                        if (settings.IsCircle)
                        {
                            imageFactory.RoundedCorners(rounding);
                        }

                        imageFactory.Save(outStream);
                    }

                    imageLayer.Image = Image.FromStream(outStream, true);
                }
            }

            return imageLayer;
        }

        public IEnumerable<string> GetLines(string text, int maxLineLength = 30)
        {
            if (string.IsNullOrWhiteSpace(text)) return Enumerable.Empty<string>();

            var lines = new List<string>();
            var words = text.Split(' ');

            var line = "";
            foreach (var word in words)
            {
                if (line.Length + word.Length >= maxLineLength)
                {
                    lines.Add(line);
                    line = "";
                }

                line += word + " ";
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }

            return lines;
        }

        public IEnumerable<TextLayer> GetTextLayersFromLines(IEnumerable<string> lines, TextLayerSettings settings, int titleOffset)
        {
            if (lines == null || !lines.Any()) return Enumerable.Empty<TextLayer>();

            var textLayers = new List<TextLayer>();

            var y = settings.YPosition + titleOffset;
            foreach (var lineItem in lines)
            {
                settings.Text = lineItem;
                settings.YPosition = y;
                textLayers.Add(GetTextLayer(settings));
                y += settings.FontSize;
            }

            return textLayers;
        }


        public IEnumerable<ImageLayer> GetAllImageLayers(DynamicImageSettings settings)
        {
            var imageLayers = new List<ImageLayer>();
            if (settings.HasAuthorImage)
            {
                var authorImage = GetAuthorImageLayer(settings.AuthorImage);
                if (authorImage != null)
                {
                    imageLayers.Add(authorImage);
                }
            }
            return imageLayers;
        }

        public byte[] GenerateImageAsBytes(DynamicImageSettings settings)
        {
            byte[] photoBytes = GetImageBytesFromUrl(settings.BackgroundImageUrl);
            ISupportedImageFormat format = new PngFormat() { Quality = settings.BackgroundImageQuality };
            using (MemoryStream inStream = new MemoryStream(photoBytes))
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    ApplyLayersToMemoryStream(settings, format, inStream, outStream);

                    return outStream.ToArray();
                }
            }
        }

        public Udi GenerateImageAsMediaItem(DynamicImageSettings settings, int parentFolderId, string umbracoMediaAlias)
        {
            byte[] photoBytes = GetImageBytesFromUrl(settings.BackgroundImageUrl);
            ISupportedImageFormat format = new PngFormat() { Quality = settings.BackgroundImageQuality };
            using (MemoryStream inStream = new MemoryStream(photoBytes))
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    ApplyLayersToMemoryStream(settings, format, inStream, outStream);

                    var imageName = settings.Title.Text;
                    var mediaItem = _mediaService.CreateMedia(imageName, parentFolderId, "image");
                    mediaItem.SetValue(_contentTypeBaseServiceProvider, "umbracoFile", imageName.ToUrlSegment() + ".png", outStream);

                    _mediaService.Save(mediaItem);

                    var udi = Udi.Create(umbracoMediaAlias, mediaItem.Key);

                    return udi;
                }
            }
        }

        public void ApplyLayersToMemoryStream(DynamicImageSettings settings, ISupportedImageFormat format, MemoryStream inStream, MemoryStream outStream)
        {
            using (ImageFactory imageFactory = new ImageFactory(preserveExifData: false))
            {
                imageFactory.Load(inStream)
                    .Format(format);

                if (settings.ImageLayers != null && settings.ImageLayers.Any())
                {
                    foreach (var imageLayer in settings.ImageLayers)
                    {
                        imageFactory.Overlay(imageLayer);
                    }
                }

                if (settings.TextLayers != null && settings.TextLayers.Any())
                {
                    foreach (var textLayer in settings.TextLayers)
                    {
                        imageFactory.Watermark(textLayer);
                    }
                }

                imageFactory.Save(outStream);
            }
        }

        public IEnumerable<TextLayer> GetAllTextLayers(DynamicImageSettings settings)
        {
            const int FirstNameIndex = 0;
            const int LastNameIndex = 1;

            List<TextLayer> allTextLayers = new List<TextLayer>();

            var titleLayers = GetTitleLayers(settings.Title);

            if (titleLayers != null && titleLayers.Any())
            {
                allTextLayers.AddRange(titleLayers);
            }

            var dateLayer = GetTextLayer(settings.Date);

            if (dateLayer != null)
            {
                allTextLayers.Add(dateLayer);
            }

            if (!string.IsNullOrWhiteSpace(settings?.AuthorName?.Text ?? ""))
            {
                var authorNames = settings.AuthorName.Text.Split(' ');

                var firstNameLayer = GetNameLayer(authorNames[FirstNameIndex], settings.AuthorName, 0);
                if (firstNameLayer != null)
                {
                    allTextLayers.Add(firstNameLayer);
                }

                var lastNameLayer = GetNameLayer(authorNames[LastNameIndex], settings.AuthorName, settings.AuthorName.LineHeight);
                if (lastNameLayer != null)
                {
                    allTextLayers.Add(lastNameLayer);
                }
            }
            return allTextLayers;
        }

        private ImageLayer GetAuthorImageLayer(ImageLayerSettings imageSettings)
        {
            var authorImage = GetImageLayer(imageSettings);
            authorImage.Position = new Point(imageSettings.XPosition, imageSettings.YPosition);
            return authorImage;
        }

        private IEnumerable<TextLayer> GetTitleLayers(TextLayerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings?.Text ?? "")) return Enumerable.Empty<TextLayer>();

            var titleLines = GetLines(settings.Text, maxLineLength: settings.MaxLineLength);
            var titleOffset = GetTitleOffset(settings.FontSize, titleLines.Count(), settings.MaxLines);
            var titleTextLayers = GetTextLayersFromLines(titleLines, settings, titleOffset);
            return titleTextLayers;
        }

        public byte[] GetImageBytesFromUrl(string imageUrl)
        {
            using (var webClient = new WebClient())
            {
                byte[] imageBytes = webClient.DownloadData(imageUrl);
                return imageBytes;
            }
        }

        private TextLayer GetNameLayer(string name, TextLayerSettings settings, int yOffset)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var firstNameOffset = GetNameOffset(settings.FontSize, name.Length, settings.MaxLineLength);
            var firstNameLayer = GetTextLayer(name, settings.Colour, settings.FontSize, settings.XPosition + firstNameOffset, settings.YPosition + yOffset, settings.FontFamily);
            return firstNameLayer;
        }

        public TextLayer GetTextLayer(string text, string colour, int fontSize, int x, int y, string fontFamily, bool dropShadow = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var textLayer = new TextLayer()
            {
                Text = text,
                FontColor = ColorTranslator.FromHtml("#" + colour),
                FontSize = fontSize,
                Position = new Point(x, y),
                FontFamily = new FontFamily(fontFamily),
                DropShadow = dropShadow
            };

            return textLayer;
        }

        private int GetNameOffset(int fontSize, int nameLength, int nameMaxLength)
        {
            var difference = nameMaxLength - nameLength;
            switch (difference)
            {
                case 0:
                    return 0;
                default:
                    return (((int)(fontSize * 0.5)) * difference) / 2;
            }
        }

        private int GetTitleOffset(int fontSize, int lineCount, int maxLines)
        {
            var difference = maxLines - lineCount;
            switch (difference)
            {
                case 0:
                    return 0;
                default:
                    return (fontSize * difference) / 2;
            }
        }

        public DynamicImageSettings GetDefaultDynamicImageSettings(string FontFamily, string title, string authorName, string authorImageUrl, DateTime postDate, string backgroundImageUrl)
        {
            return new DynamicImageSettings
            {
                BackgroundImageUrl = backgroundImageUrl,
                BackgroundImageQuality = 70,

                AuthorName = new TextLayerSettings()
                {
                    Text = authorName,
                    Colour = "000000",
                    XPosition = 869,
                    YPosition = 460,
                    FontSize = 40,
                    MaxLineLength = 10,
                    LineHeight = 50,
                    MaxLines = 2,
                    FontFamily = FontFamily,
                    DropShadow = false
                },

                Title = new TextLayerSettings()
                {
                    Text = title,
                    Colour = "fb6340",
                    XPosition = 70,
                    YPosition = 170,
                    FontSize = 60,
                    MaxLineLength = 20,
                    LineHeight = 70,
                    MaxLines = 5,
                    FontFamily = FontFamily,
                    DropShadow = true
                },

                Date = new TextLayerSettings()
                {
                    Text = postDate.ToString("dd MMM yyyy"),
                    Colour = "000000",
                    XPosition = 134,
                    YPosition = 524,
                    FontSize = 36,
                    MaxLineLength = 20,
                    LineHeight = 46,
                    MaxLines = 1,
                    FontFamily = FontFamily,
                    DropShadow = false
                },

                AuthorImage = new ImageLayerSettings()
                {
                    Url = authorImageUrl,
                    XPosition = 850,
                    YPosition = 190,
                    Width = 250,
                    Height = 250,
                    IsCircle = true
                }
            };
        }
    }
}
