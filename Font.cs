using OpenTK.Mathematics;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;

namespace KrutolFramework.Core
{
    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    public class FontGlyph
    {
        public TextureRegion Region;
        public float HorizontalAdvance;
        public float BearingX;
        public float BearingY;
        public float Width;
        public float Height;
    }

    public class FontRenderer
    {
        private readonly Dictionary<char, FontGlyph> _glyphs = [];
        public float LineSpacing { get; private set; }

        public FontRenderer(DynamicTextureAtlas atlas, string fontNameOrPath, int fontSize)
        {
            Font font;
            if (File.Exists(fontNameOrPath))
            {
                var collection = new FontCollection();
                var family = collection.Add(fontNameOrPath);
                font = family.CreateFont(fontSize, FontStyle.Regular);
            }
            else
            {
                if (SystemFonts.TryGet(fontNameOrPath, out var family))
                {
                    font = family.CreateFont(fontSize, FontStyle.Regular);
                }
                else
                {
                    font = SystemFonts.CreateFont(SystemFonts.Collection.Families.GetEnumerator().Current.Name, fontSize);
                    Console.WriteLine($"[Font] Шрифт '{fontNameOrPath}' не найден. Используется системный по умолчанию.");
                }
            }

            var fontMetrics = font.FontMetrics;
            float scaleFactor = (float)fontSize / fontMetrics.UnitsPerEm;
            LineSpacing = fontMetrics.HorizontalMetrics.LineHeight * scaleFactor;

            string charSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,!?-+=()_/\\:;@#абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ ";
            var textOptions = new TextOptions(font);

            foreach (char c in charSet)
            {
                string charStr = c.ToString();
                FontRectangle textMetrics = TextMeasurer.MeasureBounds(charStr, textOptions);

                int paddedWidth = (int)Math.Ceiling(textMetrics.Width) + 4;
                int paddedHeight = (int)Math.Ceiling(textMetrics.Height) + 4;

                if (paddedWidth <= 4 || paddedHeight <= 4)
                {
                    _glyphs[c] = new FontGlyph
                    {
                        Region = new TextureRegion { Width = 0, Height = 0 },
                        HorizontalAdvance = fontSize * 0.35f,
                        BearingX = 0,
                        BearingY = 0,
                        Width = 0,
                        Height = 0
                    };
                    continue;
                }

                byte[] pixelData = new byte[paddedWidth * paddedHeight * 4];
                using (var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(paddedWidth, paddedHeight))
                {
                    var renderOptions = new RichTextOptions(font)
                    {
                        Origin = new System.Numerics.Vector2(-textMetrics.Left, -textMetrics.Top)
                    };

                    img.Mutate(ctx => ctx.Paint(canvas =>
                    {
                        canvas.DrawText(renderOptions, charStr, SixLabors.ImageSharp.Drawing.Processing.Brushes.Solid(SixLabors.ImageSharp.Color.White), pen: null);
                    }));

                    img.CopyPixelDataTo(pixelData);
                }

                string glyphKey = $"font_{fontNameOrPath}_{fontSize}_{c}";
                TextureRegion region = atlas.RegisterRawPixels(glyphKey, paddedWidth, paddedHeight, pixelData);

                _glyphs[c] = new FontGlyph
                {
                    Region = region,
                    HorizontalAdvance = textMetrics.Width,
                    BearingX = textMetrics.Left,
                    BearingY = textMetrics.Top,
                    Width = textMetrics.Width,
                    Height = textMetrics.Height
                };
            }
        }

        /// <summary>
        /// Вычисляет размеры переданной строки (длину самой широкой подстроки и общую высоту)
        /// </summary>
        public Vector2 MeasureString(string text, Vector2 scale)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.Zero;

            float maxWidth = 0f;
            float currentWidth = 0f;
            int linesCount = 1;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    if (currentWidth > maxWidth) maxWidth = currentWidth;
                    currentWidth = 0f;
                    linesCount++;
                    continue;
                }

                if (_glyphs.TryGetValue(c, out var glyph))
                {
                    currentWidth += (glyph.HorizontalAdvance + 1f) * scale.X;
                }
            }
            if (currentWidth > maxWidth) maxWidth = currentWidth;

            return new Vector2(maxWidth, linesCount * LineSpacing * scale.Y);
        }

        public void DrawText(SpriteBatch batch, string text, Vector2 position, Vector2 scale, Color4 color, TextAlignment alignment = TextAlignment.Left)
        {
            if (string.IsNullOrEmpty(text)) return;

            string[] lines = text.Split('\n');
            float currentY = position.Y;

            foreach (var line in lines)
            {
                Vector2 lineSize = MeasureString(line, scale);
                float startX = position.X;

                // Смещение стартовой позиции каретки X в зависимости от типа выравнивания
                if (alignment == TextAlignment.Center)
                {
                    startX -= lineSize.X * 0.5f;
                }
                else if (alignment == TextAlignment.Right)
                {
                    startX -= lineSize.X;
                }

                float currentX = startX;

                foreach (char c in line)
                {
                    if (_glyphs.TryGetValue(c, out var glyph))
                    {
                        if (glyph.Region.Width > 0 && glyph.Region.Height > 0)
                        {
                            Vector2 offsetPos = new(
                                currentX + (glyph.BearingX * scale.X),
                                currentY + (glyph.BearingY * scale.Y)
                            );
                            batch.Draw(glyph.Region, offsetPos, scale, 0f, color);
                        }
                        currentX += (glyph.HorizontalAdvance + 1f) * scale.X;
                    }
                }

                currentY += LineSpacing * scale.Y;
            }
        }
    }
}
