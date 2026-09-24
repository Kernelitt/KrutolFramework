using KrutolFramework;
using KrutolFramework.Core;
using OpenTK.Mathematics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using Color = SixLabors.ImageSharp.Color;
using Font = SixLabors.Fonts.Font;

namespace KrutolFramework.Core
{
    public class FontGlyph
    {
        public TextureRegion Region;
        public float HorizontalAdvance;
        public float BearingX;
        public float BearingY;
    }

    public class FontRenderer
    {
        private readonly Dictionary<char, FontGlyph> _glyphs = new();
        public float LineSpacing { get; private set; }

        public FontRenderer(TextureAtlas atlas, string fontNameOrPath, int fontSize)
        {
            Font font;

            // 1. Загрузка шрифта
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

            // Извлечение межстрочного интервала из метрик
            var fontMetrics = font.FontMetrics;
            LineSpacing = fontMetrics.HorizontalMetrics.LineHeight * fontSize / fontMetrics.ScaleFactor;

            string charSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,!?-+=()_/\\:;@#абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ ";

            var textOptions = new TextOptions(font);

            foreach (char c in charSet)
            {
                string charStr = c.ToString();

                // Измеряем границы символа (возвращает FontRectangle)
                FontRectangle textMetrics = TextMeasurer.MeasureBounds(charStr, textOptions);

                // Получаем доступ к Span для ReadOnlyMemory во избежание ошибки CS0021
                var glyphMetricsMemory = TextMeasurer.GetGlyphMetrics(charStr, textOptions);
                if (glyphMetricsMemory.Length == 0) continue;
                var glyphMetrics = glyphMetricsMemory.Span[0];

                int width = (int)Math.Ceiling(textMetrics.Width);
                int height = (int)Math.Ceiling(textMetrics.Height);

                // Если символ не имеет физического размера (например, пробел)
                if (width <= 0 || height <= 0)
                {
                    _glyphs[c] = new FontGlyph
                    {
                        Region = new TextureRegion { Width = 0, Height = 0 },
                        HorizontalAdvance = glyphMetrics.Advance.X,
                        BearingX = textMetrics.Left,
                        BearingY = textMetrics.Top
                    };
                    continue;
                }

                byte[] pixelData = new byte[width * height * 4];

                using (var img = new Image<Rgba32>(width, height))
                {
                    // Настраиваем опции рендеринга текста на текстуру
                    var renderOptions = new RichTextOptions(font)
                    {
                        Origin = new PointF(-textMetrics.Left, -textMetrics.Top)
                    };

                    // В ImageSharp 3.x отрисовка текста происходит СТРОГО через ctx.Paint и canvas
                    img.Mutate(ctx => ctx.Paint(canvas =>
                    {
                        canvas.DrawText(renderOptions, charStr, Brushes.Solid(Color.White), pen: null);
                    }));

                    img.CopyPixelDataTo(pixelData);
                }


                string glyphKey = $"font_{fontNameOrPath}_{fontSize}_{c}";
                TextureRegion region = atlas.RegisterRawPixels(glyphKey, width, height, pixelData);

                _glyphs[c] = new FontGlyph
                {
                    Region = region,
                    HorizontalAdvance = glyphMetrics.Advance.X,
                    BearingX = textMetrics.Left,
                    BearingY = textMetrics.Top
                };
            }
        }

        public void DrawText(SpriteBatch batch, string text, Vector2 position, Vector2 scale, Color4 color)
        {
            Vector2 currentPos = position;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    currentPos.X = position.X;
                    currentPos.Y += LineSpacing * scale.Y;
                    continue;
                }

                if (_glyphs.TryGetValue(c, out var glyph))
                {
                    if (glyph.Region.Width > 0 && glyph.Region.Height > 0)
                    {
                        Vector2 offsetPos = new Vector2(
                            currentPos.X + glyph.BearingX * scale.X,
                            currentPos.Y + (LineSpacing + glyph.BearingY) * scale.Y
                        );

                        batch.Draw(glyph.Region, offsetPos, scale, 0f, color);
                    }

                    currentPos.X += glyph.HorizontalAdvance * scale.X;
                }
            }
        }
    }
}
