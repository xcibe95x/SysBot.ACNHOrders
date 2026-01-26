using System;
using SixLabors.ImageSharp;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace SysBot.ACNHOrders
{
    public class DodoDraw
    {
        private string ImagePathTemplate { get; set; } = "dodo.png";
        private string FontPath { get; set; } = "dodo.ttf";
        private string ImagePathOutput => "current" + ImagePathTemplate;

        private readonly FontCollection FontCollection = new();
        private readonly FontFamily DodoFontFamily;
        private readonly Font DodoFont;
        private readonly Image BaseImage;

        public DodoDraw(float fontPercentage = 100)
        {
            DodoFontFamily = FontCollection.Add(FontPath);
            BaseImage = Image.Load(ImagePathTemplate);
            DodoFont = DodoFontFamily.CreateFont(BaseImage.Height * 0.4f * (fontPercentage/100f), FontStyle.Regular);
        }

        public string Draw(string dodo)
        {
            var position = new PointF(BaseImage.Width * 0.5f, BaseImage.Height * 0.38f);
            using (var img = BaseImage.Clone(x => x.DrawText(dodo, DodoFont, Color.White, position)))
            {
                img.Save(ImagePathOutput);
            }

            return ImagePathOutput;
        }

        public string? GetProcessedDodoImagePath()
        {
            if (File.Exists(ImagePathOutput))
                return ImagePathOutput;

            return null;
        }
    }
}
