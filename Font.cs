using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Mathematics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

using Font = SixLabors.Fonts.Font;
using Color = SixLabors.ImageSharp.Color;

namespace KrutolFramework.Core
{
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
            // Рассчитываем точный коэффициент масштабирования из EM в пиксели
            float scaleFactor = (float)fontSize / fontMetrics.UnitsPerEm;
            LineSpacing = fontMetrics.HorizontalMetrics.LineHeight * scaleFactor;

            string charSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,!?-+=()_/\\:;@#абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ ";

            var textOptions = new TextOptions(font);

            foreach (char c in charSet)
            {
                string charStr = c.ToString();

                // Измеряем границы символа (возвращает FontRectangle)
                FontRectangle textMetrics = TextMeasurer.MeasureBounds(charStr, textOptions);

                // Создаем текстурный квадрат с запасом под глиф
                int paddedWidth = (int)Math.Ceiling(textMetrics.Width) + 4;
                int paddedHeight = (int)Math.Ceiling(textMetrics.Height) + 4;

                if (paddedWidth <= 4 || paddedHeight <= 4)
                {
                    // Для пробела и невидимых символов
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

                using (var img = new Image<Rgba32>(paddedWidth, paddedHeight))
                {
                    // Настраиваем опции рендеринга текста на текстуру
                    var renderOptions = new RichTextOptions(font)
                    {
                        // Рисуем символ точно от его локального левого верхнего угла в коробке
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
                        // Исправлено: Смещение Bearing накладывается с учетом знака метрик
                        Vector2 offsetPos = new(
                            currentPos.X + (glyph.BearingX * scale.X),
                            currentPos.Y + (glyph.BearingY * scale.Y)
                        );

                        // Отрисовываем символ из атласа
                        batch.Draw(glyph.Region, offsetPos, scale, 0f, color);
                    }

                    // Сдвигаем каретку строго на ширину буквы (Advance)
                    currentPos.X += (glyph.HorizontalAdvance + 1f) * scale.X;
                }
            }
        }
    }
}
