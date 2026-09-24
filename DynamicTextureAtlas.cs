using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace KrutolFramework.Core
{
    public struct TextureRegion
    {
        public int AtlasTextureHandle;
        public int Layer;
        public float U1, V1;
        public float U2, V2;
        public int Width;
        public int Height;
        public byte[] RawRgbaData;     // Хранит пиксели только если включен флаг keepLocalPixels
    }

    public class DynamicTextureAtlas : IDisposable
    {
        public int LayerSize { get; private set; }
        public int LayersPerPage { get; private set; }

        private readonly List<int> _atlasPages = [];
        private int _currentPageIndex = 0;
        private int _currentLayerIndex = 0;

        private int _currentX = 0;
        private int _currentY = 0;
        private int _maxRowHeight = 0;
        private readonly int _padding;

        private readonly Dictionary<string, TextureRegion> _regions = [];

        public DynamicTextureAtlas(int layerSize = 1024, int layersPerPage = 2, int padding = 2)
        {
            LayerSize = layerSize;
            LayersPerPage = layersPerPage;
            _padding = padding;

            CreateNewPage();
        }
        private void CreateNewPage()
        {
            // 1. Создаем объект текстуры типа Texture2DArray (OpenGL 4.6 DSA)
            GL.CreateTextures(TextureTarget.Texture2DArray, 1, out int pageHandle);

            // ВАЖНО ДЛЯ NVIDIA: Явно глушим мипмапы до выделения хранилища
            GL.TextureParameter(pageHandle, TextureParameterName.TextureBaseLevel, 0);
            GL.TextureParameter(pageHandle, TextureParameterName.TextureMaxLevel, 0);

            // 2. Настраиваем фильтрацию
            GL.TextureParameter(pageHandle, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TextureParameter(pageHandle, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TextureParameter(pageHandle, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TextureParameter(pageHandle, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // 3. Выделяем неизменяемую память на GPU (Immutable Storage)
            // Для Texture2DArray это ЕДИНСТВЕННЫЙ способ на NVIDIA выделить пустую память без передачи null-указателя
            GL.TextureStorage3D(
                pageHandle,
                1, // Ровно 1 уровень мипмапа (базовый)
                SizedInternalFormat.Rgba8,
                LayerSize,
                LayerSize,
                LayersPerPage
            );

            _atlasPages.Add(pageHandle);

            _currentX = 0;
            _currentY = 0;
            _maxRowHeight = 0;
            _currentLayerIndex = 0;

            Console.WriteLine($"[Atlas] Страница успешно выделена на GPU. ID: {pageHandle}, Слоев: {LayersPerPage}");
        }




        private void EnsureSpace(int imgWidth, int imgHeight)
        {
            if (_currentX + imgWidth + _padding > LayerSize)
            {
                _currentX = 0;
                _currentY += _maxRowHeight + _padding;
                _maxRowHeight = 0;
            }

            if (_currentY + imgHeight + _padding > LayerSize)
            {
                _currentX = 0;
                _currentY = 0;
                _maxRowHeight = 0;
                _currentLayerIndex++;

                if (_currentLayerIndex >= LayersPerPage)
                {
                    CreateNewPage();
                    _currentPageIndex = _atlasPages.Count - 1;
                }
            }
        }

        /// <param name="keepLocalPixels">Поставьте true ТОЛЬКО для кнопок, которым нужна Pixel-Perfect коллизия</param>
        public TextureRegion RegisterTexture(string name, string filePath, bool keepLocalPixels = false)
        {
            if (_regions.TryGetValue(name, out var existing))
                return existing;

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Текстура не найдена: {filePath}");

            using var stream = File.OpenRead(filePath);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            if (image.Width > LayerSize || image.Height > LayerSize)
                throw new ArgumentException($"Текстура {name} слишком велика!");

            EnsureSpace(image.Width, image.Height);

            int activeTextureHandle = _atlasPages[_currentPageIndex];

            // Загружаем данные на GPU напрямую по хэндлу без Bind (OpenGL 4.6 DSA)
            GL.TextureSubImage3D(
                activeTextureHandle,
                0, // левел мипмапа
                _currentX, _currentY, _currentLayerIndex, // Смещения X, Y, Z (индекс слоя)
                image.Width, image.Height, 1, // Ширина, Высота, Глубина области (1 слой)
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                image.Data
            );



            float texelOffset = 0.5f; // Сдвиг на треть пикселя внутрь барьера

            var region = new TextureRegion
            {
                AtlasTextureHandle = activeTextureHandle,
                Layer = _currentLayerIndex,
                Width = image.Width,
                Height = image.Height,
                // Внедряем микро-отступ для UV во избежание полос на стыках
                U1 = (_currentX + texelOffset) / LayerSize,
                V1 = (_currentY + texelOffset) / LayerSize,
                U2 = ((_currentX + image.Width) - texelOffset) / LayerSize,
                V2 = ((_currentY + image.Height) - texelOffset) / LayerSize,
                RawRgbaData = keepLocalPixels ? image.Data : null
            };

            _currentX += image.Width + _padding;
            if (image.Height > _maxRowHeight) _maxRowHeight = image.Height;

            _regions[name] = region;
            return region;
        }

        public TextureRegion RegisterRawPixels(string key, int width, int height, byte[] rgbaData)
        {
            if (_regions.TryGetValue(key, out var existing))
                return existing;

            EnsureSpace(width, height);

            int activeTextureHandle = _atlasPages[_currentPageIndex];

            GL.TextureSubImage3D(
                activeTextureHandle,
                0,
                _currentX, _currentY, _currentLayerIndex,
                width, height, 1,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                rgbaData
            );



            var region = new TextureRegion
            {
                AtlasTextureHandle = activeTextureHandle,
                Layer = _currentLayerIndex,
                Width = width,
                Height = height,
                U1 = (float)_currentX / LayerSize,
                V1 = (float)_currentY / LayerSize,
                U2 = (float)(_currentX + width) / LayerSize,
                V2 = (float)(_currentY + height) / LayerSize,
                RawRgbaData = null
            };

            _currentX += width + _padding;
            if (height > _maxRowHeight) _maxRowHeight = height;

            _regions[key] = region;
            return region;
        }

        public TextureRegion GetRegion(string name)
        {
            if (_regions.TryGetValue(name, out var region)) return region;
            throw new KeyNotFoundException($"Регион '{name}' не найден.");
        }

        public void Dispose()
        {
            foreach (int pageHandle in _atlasPages)
            {
                if (pageHandle != 0) GL.DeleteTexture(pageHandle);
            }
            _atlasPages.Clear();
            _regions.Clear();
        }
    }
}
