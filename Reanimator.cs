using OpenTK.Mathematics;
using System;
using System.Globalization;
using System.Xml;

namespace KrutolFramework.Core
{
    public enum ReanimLoopType
    {
        PlayOnce,
        Loop,
        PlayOnceAndHold
    }

    public class ReanimTransform
    {
        public float? TransX = null;
        public float? TransY = null;
        public float? SkewX = null;
        public float? SkewY = null;
        public float? ScaleX = null;
        public float? ScaleY = null;
        public float? Frame = null;
        public float? Alpha = null;
        public string ImageName = "";
        public string Text = "";

        public string Font { get; internal set; }
    }

    public class ReanimTrack
    {
        public string Name { get; set; } = "";
        public ReanimTransform[] Transforms { get; set; } = Array.Empty<ReanimTransform>();
    }

    public class ReanimDefinition
    {
        public float FPS { get; set; } = 12f;
        public List<ReanimTrack> Tracks { get; set; } = new();
        public int FrameCount => Tracks.Count > 0 ? Tracks[0].Transforms.Length : 0;
    }

    public class TrackInstance
    {
        public int RenderGroup = 0;
        public Color4 TrackColor = Color4.White;
        public float ShakeX = 0;
        public float ShakeY = 0;
    }


    public class Reanimation
    {
        private readonly ReanimDefinition _definition;
        private readonly DynamicTextureAtlas _atlas;
        private readonly TrackInstance[] _trackInstances;
        private readonly string _groupName;

        public float _animTime = 0f; // от 0.0 до 1.0 внутри активного диапазона
        private readonly float _animRate = 12f;

        // ИСПРАВЛЕНО: Индексы теперь динамические и могут настраиваться пользователем
        private int _frameStart = 0;
        private int _frameEnd = 0;

        // Быстрый поиск индекса трека по его имени
        private readonly Dictionary<string, int> _trackNameToIndex = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector2i> _namedAnimationBounds = new(StringComparer.OrdinalIgnoreCase);

        public Vector2 Position { get; set; } = Vector2.Zero;
        public Vector2 Scale { get; set; } = Vector2.One;
        public Color4 ColorOverride { get; set; } = Color4.White;
        public ReanimLoopType LoopType { get; set; } = ReanimLoopType.Loop;
        public bool IsDead { get; private set; } = false;
        private readonly Dictionary<string, string> _imageOverrides = new(StringComparer.OrdinalIgnoreCase);
        private float _flashTimer = 0f;
        private const float FLASH_DURATION = 0.15f; // Длительность мигания при уроне
        private TextureRegion[] _cachedTrackRegions;


        public Reanimation(ReanimDefinition definition, AssetGroup group)
        {
            _definition = definition;
            _atlas = group.Atlas;
            _groupName = group.Name;
            _animRate = _definition.FPS;

            _frameStart = 0;
            _frameEnd = _definition.FrameCount - 1;

            _trackInstances = new TrackInstance[_definition.Tracks.Count];
            for (int i = 0; i < _trackInstances.Length; i++)
            {
                _trackInstances[i] = new TrackInstance();
                _trackNameToIndex[_definition.Tracks[i].Name] = i;
            }

            // Сначала извлекаем маркеры и препроцессим данные костей
            ExtractNamedAnimationTracks();
            PreprocessDefinition();

            // ИСПРАВЛЕННЫЙ КЭШ ТЕКСТУР: Ищем картинку по всему таймлайну трека, а не только на 0-м кадра!
            _cachedTrackRegions = new TextureRegion[_definition.Tracks.Count];
            for (int i = 0; i < _definition.Tracks.Count; i++)
            {
                var track = _definition.Tracks[i];
                string foundImageName = "";

                // Пробегаем по всем кадрам трека, пока не найдем имя картинки
                for (int f = 0; f < track.Transforms.Length; f++)
                {
                    if (!string.IsNullOrEmpty(track.Transforms[f].ImageName))
                    {
                        foundImageName = track.Transforms[f].ImageName;
                        break; // Нашли, выходим из внутреннего цикла
                    }
                }

                if (!string.IsNullOrEmpty(foundImageName))
                {
                    string queryName = $"{_groupName}/{foundImageName}";
                    try
                    {
                        _cachedTrackRegions[i] = _atlas.GetRegion(queryName);

                        // Проверка на то, что атлас реально отдал текстуру
                        if (_cachedTrackRegions[i].AtlasTextureHandle == 0)
                        {
                            Console.WriteLine($"[Reanimation Warning] Атлас вернул пустой хэндл для трека '{track.Name}' (искали: '{queryName}')");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Reanimation Atlas Error] Ошибка поиска региона для трека '{track.Name}' ('{queryName}'): {ex.Message}");
                    }
                }
            }
        }


        private void ExtractNamedAnimationTracks()
        {
            _namedAnimationBounds.Clear();

            foreach (var track in _definition.Tracks)
            {
                // 1. Проверяем, является ли имя ТРЕКА маркером анимации
                if (track.Name.StartsWith("anim_", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(track.Name);
                    string cleanBoundsName = track.Name.Trim().ToLower();
                    int startFrame = -1;
                    int endFrame = -1;

                    // 2. Сканируем таймлайн этого трека, чтобы найти реальные границы
                    for (int i = 0; i < track.Transforms.Length; i++)
                    {
                        var t = track.Transforms[i];

                        // В движке PopCap кадр считается валидным, если f >= 0
                        if (t.Frame != null && t.Frame.Value >= 0f)
                        {
                            if (startFrame == -1)
                            {
                                startFrame = i; // Нашли первый кадр, где анимация начинается
                            }
                            endFrame = i; // Двигаем конечный кадр вперед, пока идут валидные данные
                        }
                        // Если встретили f = -1, это явный маркер окончания анимации в PopCap
                        else if (t.Frame != null && t.Frame.Value == -1f)
                        {
                            if (startFrame != -1)
                            {
                                break; // Анимация закончилась, выходим из поиска для этого трека
                            }
                        }
                    }

                    // 3. Если трек не пустой и мы нашли его живые кадры
                    if (startFrame != -1)
                    {
                        if (_namedAnimationBounds.TryGetValue(cleanBoundsName, out Vector2i existingBounds))
                        {
                            int currentLength = endFrame - startFrame;
                            int existingLength = existingBounds.Y - existingBounds.X;

                            if (currentLength < existingLength)
                            {
                                _namedAnimationBounds[cleanBoundsName] = new Vector2i(startFrame, endFrame);
                            }
                        }
                        else
                        {
                            _namedAnimationBounds[cleanBoundsName] = new Vector2i(startFrame, endFrame);
                        }

                        Console.WriteLine($"[Reanimation] Успешно зарегистрирован трек-маркер '{cleanBoundsName}': кадры {startFrame} - {endFrame}");
                    }
                }
            }
        }




        /// <summary>
        /// Новая удобная перегрузка: устанавливает диапазон кадров по имени служебного трека из XML
        /// </summary>
        public void SetFrameBounds(string animationName)
        {
            if (_namedAnimationBounds.TryGetValue(animationName, out Vector2i bounds))
            {
                SetFrameBounds(bounds.X, bounds.Y);
            }
            else
            {
                Console.WriteLine($"[Reanimation Error] Не удалось найти маркер анимации с именем '{animationName}'!");
                // Запасной безопасный вариант — сбросить на полную длину
                SetFrameBounds(0, _definition.FrameCount - 1);
            }
        }
        public void SetStaticFrame(string animationName, bool isLast)
        {
            if (_namedAnimationBounds.TryGetValue(animationName, out Vector2i bounds))
            {
                if (!isLast)
                {
                    SetFrameBounds(bounds.X, bounds.X);
                }
                else
                {
                    SetFrameBounds(bounds.Y, bounds.Y);
                }
            }
            else
            {
                Console.WriteLine($"[Reanimation Error] Не удалось найти маркер анимации с именем '{animationName}'!");
                // Запасной безопасный вариант — сбросить на полную длину
                SetFrameBounds(0, _definition.FrameCount - 1);
            }
        }

        public void TriggerDamageFlash() => _flashTimer = FLASH_DURATION;
        
        /// <summary>
        /// Устанавливает начальный и конечный кадры для проигрывания конкретного участка анимации.
        /// </summary>
        public void SetFrameBounds(int startFrame, int endFrame)
        {
            if (startFrame < 0 || endFrame >= _definition.FrameCount || startFrame > endFrame)
            {
                throw new ArgumentOutOfRangeException("Неверно заданы границы кадров анимации.");
            }

            _frameStart = startFrame;
            _frameEnd = endFrame;
            _animTime = 0f; // Сбрасываем время на начало нового участка
        }

        /// <summary>
        /// Полностью включает или выключает отрисовку конкретного трека по его названию.
        /// </summary>
        public void SetTrackVisible(string trackName, bool visible)
        {
            if (_trackNameToIndex.TryGetValue(trackName, out int index))
            {
                // В оригинале -1 означает полностью скрытый (Hidden) слой
                _trackInstances[index].RenderGroup = visible ? 0 : -1;
            }
            else
            {
                Console.WriteLine($"[Reanimation] Предупреждение: Трек с именем '{trackName}' не найден.");
            }
        }

        private void PreprocessDefinition()
        {
            foreach (var track in _definition.Tracks)
            {
                float prevTransX = 0f; float prevTransY = 0f;
                float prevSkewX = 0f; float prevSkewY = 0f;
                float scaleX_Global = 1f; float scaleY_Global = 1f; // Внутреннее имя изменено во избежание конфликтов

                // По умолчанию альфа всегда равна 1.0f (видимый)
                float prevAlpha = 1f;

                // Стартуем с 0f (кадр активен), пока явный маркер -1 в XML не скроет трек
                float prevFrame = 0f;
                string prevImg = "";

                // ВАЖНО: Убираем первый проход, который искал картинку "из будущего"!
                // PopCap наследует картинки строго по ходу таймлайна.

                for (int i = 0; i < track.Transforms.Length; i++)
                {
                    var t = track.Transforms[i];

                    // Наследование имени картинки (строго от предыдущего кадра к следующему)
                    if (!string.IsNullOrEmpty(t.ImageName))
                    {
                        prevImg = t.ImageName;
                    }
                    else
                    {
                        t.ImageName = prevImg;
                    }

                    // Обработка номера кадра (Frame)
                    if (t.Frame != null)
                    {
                        prevFrame = t.Frame.Value;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(t.ImageName) && !track.Name.StartsWith("anim_", StringComparison.OrdinalIgnoreCase))
                        {
                            t.Frame = -1f;
                        }
                        else
                        {
                            t.Frame = prevFrame;
                        }
                        prevFrame = t.Frame.Value;
                    }

                    // Обработка альфа-канала (прозрачности)
                    if (t.Alpha != null)
                    {
                        prevAlpha = t.Alpha.Value;
                    }
                    else
                    {
                        t.Alpha = prevAlpha;
                    }

                    // Наследование стандартных трансформаций костей
                    if (t.TransX == null) t.TransX = prevTransX; else prevTransX = t.TransX.Value;
                    if (t.TransY == null) t.TransY = prevTransY; else prevTransY = t.TransY.Value;
                    if (t.SkewX == null) t.SkewX = prevSkewX; else prevSkewX = t.SkewX.Value;
                    if (t.SkewY == null) t.SkewY = prevSkewY; else prevSkewY = t.SkewY.Value;
                    if (t.ScaleX == null) t.ScaleX = scaleX_Global; else scaleX_Global = t.ScaleX.Value;
                    if (t.ScaleY == null) t.ScaleY = scaleY_Global; else scaleY_Global = t.ScaleY.Value;

                    // Записываем очищенное состояние обратно в массив
                    track.Transforms[i] = t;
                }
            }
        }



        public bool HasMarker(string animationName) => _namedAnimationBounds.ContainsKey(animationName);

        public void Update(float deltaTime)
        {
            if (IsDead || _definition.FrameCount == 0) return;

            int activeIntervals = _frameEnd - _frameStart;

            if (_flashTimer > 0f)
            {
                _flashTimer -= deltaTime; 
                if (_flashTimer < 0f) _flashTimer = 0f;
            }

            if (activeIntervals <= 0)
            {
                _animTime = 0f;
                return;
            }

            // Движение по таймлайну на основе интервалов диапазона
            _animTime += deltaTime * _animRate / activeIntervals;

            if (_animTime >= 1.0f)
            {
                if (LoopType == ReanimLoopType.Loop)
                {
                    // Плавно сбрасываем время в ноль, сохраняя остаток шага времени
                    _animTime %= 1.0f;
                }
                else if (LoopType == ReanimLoopType.PlayOnce)
                {
                    _animTime = 1.0f;
                    IsDead = true;
                }
                else if (LoopType == ReanimLoopType.PlayOnceAndHold)
                {
                    _animTime = 0.999f;
                }
            }
        }
        private Vector2 _v0, _v1, _v2, _v3;

        public void Render(SpriteBatch batch)
        {
            if (IsDead || _definition.FrameCount == 0) return;

            int activeIntervals = (_frameEnd - _frameStart) + 1;
            if (activeIntervals <= 0) activeIntervals = 1;

            float positionInTracks = _frameStart + _animTime * (activeIntervals - 1); // Интерполируем внутри живых кадров
            float aAnimFrameBefore = MathF.Floor(positionInTracks);

            float fraction = positionInTracks - aAnimFrameBefore;
            int frameBefore = (int)aAnimFrameBefore;

            // ВАЖНО: frameAfter жестко ограничивается рамками текущей фазы (_frameEnd)
            int frameAfter = frameBefore + 1;
            if (frameAfter > _frameEnd)
            {
                frameAfter = _frameEnd;
                fraction = 0f; // Прекращаем интерполяцию, мы на финише
            }

            frameBefore = Math.Clamp(frameBefore, _frameStart, _frameEnd);

            if (frameBefore >= _frameEnd) { frameBefore = _frameEnd; frameAfter = _frameEnd; fraction = 0f; }
            if (frameAfter > _frameEnd) { frameAfter = frameBefore; fraction = 0f; }
            if (frameBefore < _frameStart) frameBefore = _frameStart;

            // Включаем или выключаем белый flash на шейдере батча
            float flashIntensity = _flashTimer > 0f ? (_flashTimer / FLASH_DURATION) * 0.75f : 0f;

            float baseAlpha = ColorOverride.A;

            // Кэшируем значения масштаба для цикла
            float scaleX_Global = Scale.X;
            float scaleY_Global = Scale.Y;
            float posX_Global = Position.X;
            float posY_Global = Position.Y;

            // Быстрый перевод градусов в радианы одной операцией
            const float degToRad = MathF.PI / 180.0f;

            for (int i = 0; i < _definition.Tracks.Count; i++)
            {
                var instance = _trackInstances[i];
                if (instance.RenderGroup == -1) continue;

                var track = _definition.Tracks[i];
                var tBefore = track.Transforms[frameBefore];

                if (tBefore.Frame == null || tBefore.Frame < 0f || string.IsNullOrEmpty(tBefore.ImageName)) continue;

                TextureRegion region;
                string originalImageName = tBefore.ImageName;

                if (_imageOverrides.Count > 0 && _imageOverrides.TryGetValue(originalImageName, out string? overriddenName))
                {
                    region = _atlas.GetRegion($"{_groupName}/{overriddenName}");
                }
                else
                {
                    region = _cachedTrackRegions[i];
                    if (region.AtlasTextureHandle == 0)
                    {
                        region = _atlas.GetRegion($"{_groupName}/{originalImageName}");
                    }
                }

                // КРИТИЧЕСКИЙ ВЫВОД: Если регион так и не найден, пишем в консоль имя потерянного ассета
                if (region.AtlasTextureHandle == 0)
                {
                    // Чтобы не засорять лог 60 раз в секунду, выводим только на первом кадре фазы
                    if (frameBefore == _frameStart)
                    {
                        Console.WriteLine($"[Reanimation Critical] Трек '{track.Name}' не отрисован! Текстура '{_groupName}/{originalImageName}' отсутствует в атласе.");
                    }
                    continue;
                }

                var tAfter = track.Transforms[frameAfter];

                // Линейная интерполяция (SIMD здесь не нужен, но пишем в одну строку для инлайнинга компилятором)
                float alpha = tBefore.Alpha.Value + (tAfter.Alpha.Value - tBefore.Alpha.Value) * fraction;
                float finalAlpha = alpha * instance.TrackColor.A * baseAlpha;
                if (finalAlpha <= 0.001f) continue;

                float transX = tBefore.TransX.Value + (tAfter.TransX.Value - tBefore.TransX.Value) * fraction;
                float transY = tBefore.TransY.Value + (tAfter.TransY.Value - tBefore.TransY.Value) * fraction;
                float skewX = tBefore.SkewX.Value + (tAfter.SkewX.Value - tBefore.SkewX.Value) * fraction;
                float skewY = tBefore.SkewY.Value + (tAfter.SkewY.Value - tBefore.SkewY.Value) * fraction;
                float scaleX = tBefore.ScaleX.Value + (tAfter.ScaleX.Value - tBefore.ScaleX.Value) * fraction;
                float scaleY = tBefore.ScaleY.Value + (tAfter.ScaleY.Value - tBefore.ScaleY.Value) * fraction;

                float radSkewX = -(skewX * degToRad);
                float radSkewY = -(skewY * degToRad);

                // ОПТИМИЗАЦИЯ: Компилятор .NET 8+ автоматически заменяет Sin и Cos на один системный вызов SinCos, если они идут подряд
                float cosSkewX = MathF.Cos(radSkewX);
                float sinSkewX = MathF.Sin(radSkewX);
                float cosSkewY = MathF.Cos(radSkewY);
                float sinSkewY = MathF.Sin(radSkewY);

                float m11 = cosSkewX * scaleX * scaleX_Global;
                float m12 = -sinSkewX * scaleX * scaleY_Global;
                float m21 = sinSkewY * scaleY * scaleX_Global;
                float m22 = cosSkewY * scaleY * scaleY_Global;
                float m31 = transX * scaleX_Global + posX_Global;
                float m32 = transY * scaleY_Global + posY_Global;

                float w = region.Width;
                float h = region.Height;

                // ОПТИМИЗАЦИЯ: Избегаем создания новых объектов структур на куче/стеке
                _v0.X = m31;
                _v0.Y = m32;

                _v1.X = w * m11 + m31;
                _v1.Y = w * m12 + m32;

                _v2.X = w * m11 + h * m21 + m31;
                _v2.Y = w * m12 + h * m22 + m32;

                _v3.X = h * m21 + m31;
                _v3.Y = h * m22 + m32;

                batch.DrawDirectMatrix(region, _v0, _v1, _v2, _v3, new Color4(ColorOverride.R, ColorOverride.G, ColorOverride.B, finalAlpha), flashIntensity);
            }
        }


        public Vector2 GetTrackPosition(string trackName)
        {
            if (!_trackNameToIndex.TryGetValue(trackName, out int index) || _definition.FrameCount == 0)
                return Position; // Возвращаем базовую точку, если трек не найден

            int activeIntervals = _frameEnd - _frameStart;
            float clampedTime = _animTime % 1.0f;
            if (clampedTime < 0f) clampedTime += 1.0f;

            float positionInTracks = _frameStart + clampedTime * activeIntervals;
            int frameBefore = (int)MathF.Floor(positionInTracks);
            int frameAfter = frameBefore + 1;
            float fraction = positionInTracks - frameBefore;

            if (frameBefore >= _frameEnd) { frameBefore = _frameEnd; frameAfter = _frameEnd; fraction = 0f; }
            if (frameAfter > _frameEnd) { frameAfter = frameBefore; fraction = 0f; }
            if (frameBefore < _frameStart) frameBefore = _frameStart;

            var track = _definition.Tracks[index];
            var tBefore = track.Transforms[frameBefore];
            var tAfter = track.Transforms[frameAfter];

            // Интерполируем локальные координаты трека
            float transX = tBefore.TransX.Value + (tAfter.TransX.Value - tBefore.TransX.Value) * fraction;
            float transY = tBefore.TransY.Value + (tAfter.TransY.Value - tBefore.TransY.Value) * fraction;

            // Переводим в глобальные экранные координаты с учетом масштаба всей анимации
            return new Vector2(transX * Scale.X + Position.X, transY * Scale.Y + Position.Y);
        }
        public void OverrideTrackImage(string originalImageName, string newImageName)
        {
            if (string.IsNullOrEmpty(newImageName))
            {
                _imageOverrides.Remove(originalImageName);
            }
            else
            {
                _imageOverrides[originalImageName] = newImageName;
            }
        }
    }

    public static class ReanimParser
    {
        public static ReanimDefinition ParseXml(string filePath)
        {
            var def = new ReanimDefinition();
            var doc = new XmlDocument();

            string rawXml = File.ReadAllText(filePath);
            string validXml = $"<reanim_file>{rawXml}</reanim_file>";
            doc.LoadXml(validXml);

            XmlNode root = doc.SelectSingleNode("reanim_file");

            XmlNode fpsNode = root.SelectSingleNode("fps");
            if (fpsNode != null)
            {
                float.TryParse(fpsNode.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out float fps);
                def.FPS = fps;
            }

            XmlNodeList trackNodes = root.SelectNodes("track");
            if (trackNodes != null)
            {
                foreach (XmlNode trackNode in trackNodes)
                {
                    var track = new ReanimTrack();
                    XmlNode nameNode = trackNode.SelectSingleNode("name");
                    track.Name = nameNode != null ? nameNode.InnerText.Trim() : "unknown_track";

                    XmlNodeList transformNodes = trackNode.SelectNodes("t");
                    if (transformNodes != null)
                    {
                        var transformsList = new List<ReanimTransform>();
                        foreach (XmlNode tNode in transformNodes)
                        {
                            var t = new ReanimTransform
                            {
                                TransX = ReadFloatChild(tNode, "x"),
                                TransY = ReadFloatChild(tNode, "y"),
                                ScaleX = ReadFloatChild(tNode, "sx"),
                                ScaleY = ReadFloatChild(tNode, "sy"),
                                SkewX = ReadFloatChild(tNode, "kx"),
                                SkewY = ReadFloatChild(tNode, "ky"),
                                Frame = ReadFloatChild(tNode, "f"),
                                Alpha = ReadFloatChild(tNode, "a"),

                                ImageName = tNode.SelectSingleNode("i")?.InnerText?.Trim() ?? "",
                                Text = tNode.SelectSingleNode("text")?.InnerText?.Trim() ?? ""
                            };

                            transformsList.Add(t);
                        }
                        track.Transforms = transformsList.ToArray();
                    }

                    def.Tracks.Add(track);
                }
            }

            return def;
        }

        // ИСПРАВЛЕНО: Метод теперь возвращает float? (null при отсутствии тега)
        private static float? ReadFloatChild(XmlNode parentNode, string childName)
        {
            XmlNode child = parentNode.SelectSingleNode(childName);
            if (child == null) return null;

            if (float.TryParse(child.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
            return null;
        }
    }
}
