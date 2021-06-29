using ImageProcessor.Imaging;
using System.Collections.Generic;

namespace Our.Umbraco.DynamicImages.Settings
{
    public class DynamicImageSettings
    {
        public string BackgroundImageUrl { get; set; }
        public int BackgroundImageQuality { get; set; }
        public ImageLayerSettings AuthorImage { get; set; }
        public TextLayerSettings AuthorName { get; set; }
        public TextLayerSettings Title { get; set; }
        public TextLayerSettings Date { get; set; }
        public bool HasAuthorImage => !string.IsNullOrWhiteSpace(AuthorImage?.Url ?? "");
        public IEnumerable<TextLayer> TextLayers { get; set; }
        public IEnumerable<ImageLayer> ImageLayers { get; set; }
    }
}