using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace KrutolFramework.Core
{
    /// <summary>
    /// Регион текстуры внутри массива Texture2DArray.
    /// Хранит слой и UV-координаты, а также сырые пиксели для Pixel-Perfect коллизий.
    /// </summary>
    public struct TextureRegion
    {
        public int AtlasTextureHandle; // OpenGL Handle массива текстур
        public int Layer;               // Индекс слоя в массиве (Z-координата)
        public float U1, V1;           // Левый верхний угол (UV)
        public float U2, V2;           // Правый нижний угол (UV)
        public int Width;              // Исходная ширина в пикселях
        public int Height;             // Исходная высота в пикселях
        public byte[] RawRgbaData;     // Пиксели в RAM (RGBA), используемые GUI элементами для точной коллизии
    }

    public class TextureAtlas : IDisposable
    {
        public int GLTextureHandle { get; private set; }
        public int LayerSize { get; private set; }
        public int MaxLayers { get; private set; }

        private int _currentLayerCount = 1;
        private int _currentX = 0;
        private int _currentY = 0;
        private int _maxRowHeight = 0;
        private readonly int _padding;

        private readonly Dictionary<string, TextureRegion> _regions = new();

        /// <summary>
        /// Создает динамический атлас текстур на базе Texture2DArray.
        /// </summary>
        /// <param name="layerSize">Размер стороны одного квадратного слоя (например, 1024 или 2048)</param>
        /// <param name="padding">Отступ между текстурами во избежание артефактов фильтрации</param>
        /// <param name="maxLayers">Максимальное количество слоев, до которого атлас может расти</param>
        public TextureAtlas(int layerSize = 2048, int padding = 2, int maxLayers = 64)
        {
            LayerSize = layerSize;
            _padding = padding;
            MaxLayers = maxLayers;

            AllocateNewAtlasArray(_currentLayerCount);
        }

        /// <summary>
        /// Выделяет или перевыделяет Texture2DArray на GPU с копированием старых данных.
        /// </summary>
        private void AllocateNewAtlasArray(int totalLayers)
        {
            int oldHandle = GLTextureHandle;

            // 1. Создаем новый объект текстуры типа Texture2DArray (OpenGL 4.6 DSA)
            GL.CreateTextures(TextureTarget.Texture2DArray, 1, out int newHandle);

            // Выделяем неизменяемое хранилище (Immutable Storage) под 3D-объем (Ширина x Высота x Слои)
            GL.TextureStorage3D(newHandle, 1, SizedInternalFormat.Rgba8, LayerSize, LayerSize, totalLayers);

            // 2. Настраиваем параметры фильтрации
            GL.TextureParameter(newHandle, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TextureParameter(newHandle, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TextureParameter(newHandle, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TextureParameter(newHandle, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // 3. Если старый атлас уже существовал, плавно мигрируем данные на GPU
            if (oldHandle != 0)
            {
                // Копируем все заполненные слои из старого массива в новый напрямую внутри видеопамяти
                GL.CopyImageSubData(
                    oldHandle, ImageTarget.Texture2DArray, 0, 0, 0, 0,
                    newHandle, ImageTarget.Texture2DArray, 0, 0, 0, 0,
                    LayerSize, LayerSize, _currentLayerCount - 1
                );

                // Удаляем старый хэндл, очищая память на GPU
                GL.DeleteTexture(oldHandle);
            }

            GLTextureHandle = newHandle;
        }

        /// <summary>
        /// Проверяет, влезает ли текстура, и если нет — переносит каретку или расширяет массив слоев на GPU.
        /// </summary>
        private void EnsureSpace(int imgWidth, int imgHeight)
        {
            // Если текстура по ширине не влезает в текущий ряд — переходим на новый ряд
            if (_currentX + imgWidth + _padding > LayerSize)
            {
                _currentX = 0;
                _currentY += _maxRowHeight + _padding;
                _maxRowHeight = 0;
            }

            // Если по высоте мы вышли за границы текущего слоя — создаем новый слой на GPU
            if (_currentY + imgHeight + _padding > LayerSize)
            {
                if (_currentLayerCount >= MaxLayers)
                {
                    throw new InvalidOperationException($"[Atlas Error] Достигнут жесткий лимит слоев ({MaxLayers}). Расширение невозможно.");
                }

                _currentLayerCount++;
                AllocateNewAtlasArray(_currentLayerCount);

                // Сбрасываем координаты упаковщика для нового чистого слоя
                _currentX = 0;
                _currentY = 0;
                _maxRowHeight = 0;

                Console.WriteLine($"[Atlas] Атлас динамически расширен. Текущее число слоев: {_currentLayerCount}");
            }
        }

        /// <summary>
        /// Загружает изображение с диска и пакует его в динамический атлас.
        /// </summary>
        public TextureRegion RegisterTexture(string name, string filePath)
        {
            if (_regions.TryGetValue(name, out var existing))
                return existing;

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Текстура для атласа не найдена: {filePath}");

            using (var stream = File.OpenRead(filePath))
            {
                // Загружаем картинку в формате RGBA
                ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                if (image.Width > LayerSize || image.Height > LayerSize)
                    throw new ArgumentException($"Размер текстуры '{name}' ({image.Width}x{image.Height}) больше максимального размера слоя атласа ({LayerSize}x{LayerSize})!");

                EnsureSpace(image.Width, image.Height);

                int targetLayer = _currentLayerCount - 1;

                // Загружаем пиксели в под-область 3D текстуры (Z = индекс слоя) через DSA
                GL.TextureSubImage3D(
                    GLTextureHandle, 0,
                    _currentX, _currentY, targetLayer,
                    image.Width, image.Height, 1,
                    PixelFormat.Rgba, PixelType.UnsignedByte, image.Data
                );

                var region = new TextureRegion
                {
                    AtlasTextureHandle = GLTextureHandle,
                    Layer = targetLayer,
                    Width = image.Width,
                    Height = image.Height,
                    U1 = (float)_currentX / LayerSize,
                    V1 = (float)_currentY / LayerSize,
                    U2 = (float)(_currentX + image.Width) / LayerSize,
                    V2 = (float)(_currentY + image.Height) / LayerSize,
                    RawRgbaData = image.Data // Сохраняем для Pixel-Perfect GUI проверок
                };

                // Сдвигаем маркер упаковщика
                _currentX += image.Width + _padding;
                if (image.Height > _maxRowHeight) _maxRowHeight = image.Height;

                _regions[name] = region;
                return region;
            }
        }

        /// <summary>
        /// Регистрирует сырой массив байт пикселей (например, сгенерированных глифов шрифта) напрямую в атлас.
        /// </summary>
        public TextureRegion RegisterRawPixels(string key, int width, int height, byte[] rgbaData)
        {
            if (_regions.TryGetValue(key, out var existing))
                return existing;

            if (width > LayerSize || height > LayerSize)
                throw new ArgumentException($"Размер сырых пикселей '{key}' превышает границы слоя атласа!");

            EnsureSpace(width, height);

            int targetLayer = _currentLayerCount - 1;

            GL.TextureSubImage3D(
                GLTextureHandle, 0,
                _currentX, _currentY, targetLayer,
                width, height, 1,
                PixelFormat.Rgba, PixelType.UnsignedByte, rgbaData
            );

            var region = new TextureRegion
            {
                AtlasTextureHandle = GLTextureHandle,
                Layer = targetLayer,
                Width = width,
                Height = height,
                U1 = (float)_currentX / LayerSize,
                V1 = (float)_currentY / LayerSize,
                U2 = (float)(_currentX + width) / LayerSize,
                V2 = (float)(_currentY + height) / LayerSize,
                RawRgbaData = rgbaData
            };

            _currentX += width + _padding;
            if (height > _maxRowHeight) _maxRowHeight = height;

            _regions[key] = region;
            return region;
        }

        /// <summary>
        /// Извлекает ранее загруженный регион по его имени.
        /// </summary>
        public TextureRegion GetRegion(string name)
        {
            if (_regions.TryGetValue(name, out var region))
                return region;
            throw new KeyNotFoundException($"Регион '{name}' отсутствует в динамическом атласе.");
        }

        public void Dispose()
        {
            if (GLTextureHandle != 0)
            {
                GL.DeleteTexture(GLTextureHandle);
                GLTextureHandle = 0;
            }
            _regions.Clear();
        }
    }
}
