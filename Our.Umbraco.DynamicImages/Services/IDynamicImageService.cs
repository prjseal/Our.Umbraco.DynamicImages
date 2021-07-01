using ImageProcessor.Imaging;
using Our.Umbraco.DynamicImages.Settings;
using System;
using System.Collections.Generic;
using Umbraco.Core;

namespace Our.Umbraco.DynamicImages.Services
{
    public interface IDynamicImageService
    {
        TextLayer GetTextLayer(TextLayerSettings settings);
        TextLayer GetTextLayer(string text, string colour, int fontSize, int x, int y, string fontFamily, bool dropShadow = false);
        IEnumerable<TextLayer> GetTitleLayers(TextLayerSettings settings);
        ImageLayer GetImageLayer(ImageLayerSettings settings);
        IEnumerable<string> GetLines(string text, int maxLineLength = 30);
        IEnumerable<TextLayer> GetTextLayersFromLines(IEnumerable<string> lines, TextLayerSettings settings, int titleOffset);
        Udi GenerateImageAsMediaItem(DynamicImageSettings settings, int parentFolderId, string umbracoMediaAlias);
        byte[] GenerateImageAsBytes(DynamicImageSettings settings);
        DynamicImageSettings GetDefaultDynamicImageSettings(string FontFamily, string title, string authorName, string authorImagePath, DateTime postDate, string backgroundImagePath);
        IEnumerable<ImageLayer> GetAllImageLayers(DynamicImageSettings settings);
        IEnumerable<TextLayer> GetAllTextLayers(DynamicImageSettings settings);
    }
}