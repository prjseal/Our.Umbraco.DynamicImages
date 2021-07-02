using ImageProcessor.Imaging;
using Our.Umbraco.DynamicImages.Settings;
using System;
using System.Collections.Generic;
using Umbraco.Core;

namespace Our.Umbraco.DynamicImages.Services
{
    public interface IDynamicImageService
    {
        byte[] GenerateImageAsBytes(DynamicImageSettings settings);
        Udi GenerateImageAsMediaItem(DynamicImageSettings settings, int parentFolderId, string umbracoMediaAlias, string imageName);
        DynamicImageSettings GetDefaultDynamicImageSettings(string FontFamily, string title, string authorName, string authorImagePath, DateTime postDate, string backgroundImagePath);
        TextLayer GetTextLayer(TextLayerSettings settings);
        TextLayer GetTextLayer(string text, string colour, int fontSize, int x, int y, string fontFamily, bool dropShadow = false);
        IEnumerable<TextLayer> GetTitleLayers(TextLayerSettings settings);
        IEnumerable<TextLayer> GetTextLayersFromLines(IEnumerable<string> lines, TextLayerSettings settings, int titleOffset);
        IEnumerable<TextLayer> GetAllTextLayers(DynamicImageSettings settings);
        ImageLayer GetImageLayer(ImageLayerSettings settings);
        IEnumerable<ImageLayer> GetAllImageLayers(DynamicImageSettings settings);
        IEnumerable<string> GetLines(string text, int maxLineLength = 30);
    }
}