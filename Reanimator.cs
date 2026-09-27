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
        public int RenderGroup = 0; // 0 - Normal, -1 - Hidden
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
        private float _animRate = 12f;

        // ИСПРАВЛЕНО: Индексы теперь динамические и могут настраиваться пользователем
        private int _frameStart = 0;
        private int _frameEnd = 0;

        // Быстрый поиск индекса трека по его имени
        private readonly Dictionary<string, int> _trackNameToIndex = new(StringComparer.OrdinalIgnoreCase);

        public Vector2 Position { get; set; } = Vector2.Zero;
        public Vector2 Scale { get; set; } = Vector2.One;
        public Color4 ColorOverride { get; set; } = Color4.White;
        public ReanimLoopType LoopType { get; set; } = ReanimLoopType.Loop;
        public bool IsDead { get; private set; } = false;

        public Reanimation(ReanimDefinition definition, AssetGroup group)
        {
            _definition = definition;
            _atlas = group.Atlas;
            _groupName = group.Name;
            _animRate = _definition.FPS;

            // По умолчанию активный диапазон — вся анимация
            _frameStart = 0;
            _frameEnd = _definition.FrameCount - 1;

            _trackInstances = new TrackInstance[_definition.Tracks.Count];
            for (int i = 0; i < _trackInstances.Length; i++)
            {
                _trackInstances[i] = new TrackInstance();
                // Заполняем карту имен для мгновенного поиска треков
                _trackNameToIndex[_definition.Tracks[i].Name] = i;
            }

            PreprocessDefinition();
        }

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
        /// Полностью включает или выключает отрисовку конкретного трека (кости/слоя) по его названию.
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
                float prevTransX = 0f;
                float prevTransY = 0f;
                float prevSkewX = 0f;
                float prevSkewY = 0f;
                float prevScaleX = 1f;
                float prevScaleY = 1f;
                float prevAlpha = 1f;

                float prevFrame = -1f; // Каждый трек изначально скрыт
                string prevImg = "";

                for (int i = 0; i < track.Transforms.Length; i++)
                {
                    var t = track.Transforms[i];

                    // 1. Фиксируем, была ли картинка записана в XML ИМЕННО для этой строки (до наследования)
                    bool stringHasExplicitImage = !string.IsNullOrEmpty(t.ImageName);

                    // 2. Наследование имени изображения по цепочке
                    if (!stringHasExplicitImage)
                    {
                        t.ImageName = prevImg;
                    }
                    else
                    {
                        prevImg = t.ImageName;
                    }

                    // 3. СТРОГИЙ ПРИОРИТЕТ ВЫЧИСЛЕНИЯ FRAME (ВИДИМОСТИ)
                    if (t.Frame != null)
                    {
                        // ПРАВИЛО 1: Если в XML явно указан <f> (например, <f>-1</f>), 
                        // мы берем его безоговорочно. Он имеет абсолютный приоритет!
                        prevFrame = t.Frame.Value;
                    }
                    else
                    {
                        // ПРАВИЛО 2: Тега <f> нет. Если в этой строке XML принудительно подсунули 
                        // НОВУЮ картинку, значит трек должен проснуться и стать видимым (0f)
                        if (stringHasExplicitImage)
                        {
                            t.Frame = 0f;
                            prevFrame = 0f;
                        }
                        else
                        {
                            // ПРАВИЛО 3: Ничего не указано — просто наследуем состояние предыдущего кадра
                            t.Frame = prevFrame;
                        }
                    }

                    // 4. Наследование стандартных матричных параметров
                    if (t.TransX == null) t.TransX = prevTransX; else prevTransX = t.TransX.Value;
                    if (t.TransY == null) t.TransY = prevTransY; else prevTransY = t.TransY.Value;
                    if (t.SkewX == null) t.SkewX = prevSkewX; else prevSkewX = t.SkewX.Value;
                    if (t.SkewY == null) t.SkewY = prevSkewY; else prevSkewY = t.SkewY.Value;
                    if (t.ScaleX == null) t.ScaleX = prevScaleX; else prevScaleX = t.ScaleX.Value;
                    if (t.ScaleY == null) t.ScaleY = prevScaleY; else prevScaleY = t.ScaleY.Value;
                    if (t.Alpha == null) t.Alpha = prevAlpha; else prevAlpha = t.Alpha.Value;

                    // 5. Маскирование картинки исключительно для рендерера текущего кадра, если он скрыт
                    if (t.Frame < 0f)
                    {
                        t.ImageName = "";
                    }
                }
            }
        }


        public void Update(float deltaTime)
        {
            if (IsDead || _definition.FrameCount == 0) return;

            // В PopCap межкадровых интервалов всегда на 1 меньше, чем общее количество кадров
            int activeIntervals = _frameEnd - _frameStart;

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

        public void Render(SpriteBatch batch)
        {
            if (IsDead || _definition.FrameCount == 0) return;

            int activeIntervals = _frameEnd - _frameStart;

            // Безопасное удержание времени в рамках [0.0, 1.0)
            float clampedTime = _animTime % 1.0f;
            if (clampedTime < 0f) clampedTime += 1.0f;

            // Расчет текущей позиции кадра
            float positionInTracks = _frameStart + clampedTime * activeIntervals;

            int frameBefore = (int)MathF.Floor(positionInTracks);
            int frameAfter = frameBefore + 1;
            float fraction = positionInTracks - frameBefore;

            // Если ушли за границы диапазона — жестко фиксируем кадры на конце
            if (frameBefore >= _frameEnd)
            {
                frameBefore = _frameEnd;
                frameAfter = _frameEnd;
                fraction = 0f;
            }

            if (frameAfter > _frameEnd)
            {
                frameAfter = frameBefore;
                fraction = 0f;
            }

            if (frameBefore < _frameStart) frameBefore = _frameStart;

            // Рендерим треки
            for (int i = 0; i < _definition.Tracks.Count; i++)
            {
                var track = _definition.Tracks[i];
                var instance = _trackInstances[i];

                if (instance.RenderGroup == -1) continue;

                var tBefore = track.Transforms[frameBefore];
                var tAfter = track.Transforms[frameAfter];

                // ИСПРАВЛЕНО: Проверяем видимость СТРОГО по текущему кадру (tBefore).
                // Если текущий кадр скрыт (null или < 0), то только тогда мы пропускаем трек.
                // Больше никакого заглядывания в tAfter.Frame, которое вызывало преждевременное исчезновение деталей!
                if (tBefore.Frame == null || tBefore.Frame < 0f)
                {
                    continue;
                }

                // Плавная интерполяция параметров движения
                float transX = MathHelper.Lerp(tBefore.TransX.Value, tAfter.TransX.Value, fraction);
                float transY = MathHelper.Lerp(tBefore.TransY.Value, tAfter.TransY.Value, fraction);
                float skewX = MathHelper.Lerp(tBefore.SkewX.Value, tAfter.SkewX.Value, fraction);
                float skewY = MathHelper.Lerp(tBefore.SkewY.Value, tAfter.SkewY.Value, fraction);
                float scaleX = MathHelper.Lerp(tBefore.ScaleX.Value, tAfter.ScaleX.Value, fraction);
                float scaleY = MathHelper.Lerp(tBefore.ScaleY.Value, tAfter.ScaleY.Value, fraction);
                float alpha = MathHelper.Lerp(tBefore.Alpha.Value, tAfter.Alpha.Value, fraction);

                // Если у tBefore стерто имя картинки (из-за f=-1), мы проверили это выше.
                // Но на всякий случай страхуем строку от пустоты перед выборкой из атласа.
                if (string.IsNullOrEmpty(tBefore.ImageName)) continue;

                TextureRegion region;
                try
                {
                    string queryName = $"{_groupName}/{tBefore.ImageName}";
                    region = _atlas.GetRegion(queryName);
                }
                catch
                {
                    continue;
                }

                Matrix3 transformMatrix = CalculatePopCapMatrix(transX, transY, skewX, skewY, scaleX, scaleY, region.Width, region.Height);
                RenderTrackQuad(batch, region, transformMatrix, alpha * instance.TrackColor.A * ColorOverride.A);
            }
        }





        private Matrix3 CalculatePopCapMatrix(float tx, float ty, float sx, float sy, float scX, float scY, float imgW, float imgH)
        {
            float radSkewX = -(sx * MathF.PI / 180.0f);
            float radSkewY = -(sy * MathF.PI / 180.0f);

            Matrix3 m = Matrix3.Identity;

            m.M11 = MathF.Cos(radSkewX) * scX;
            m.M12 = -MathF.Sin(radSkewX) * scX;
            m.M21 = MathF.Sin(radSkewY) * scY;
            m.M22 = MathF.Cos(radSkewY) * scY;
            m.M31 = tx * Scale.X + Position.X ;
            m.M32 = ty * Scale.Y + Position.Y ;

            m.M11 *= Scale.X; m.M12 *= Scale.Y;
            m.M21 *= Scale.X; m.M22 *= Scale.Y;

            return m;
        }

        private void RenderTrackQuad(SpriteBatch batch, TextureRegion region, Matrix3 mat, float finalAlpha)
        {
            float w = region.Width;
            float h = region.Height;

            // В оригинальном движке PopCap PvZ опорная точка (Pivot) для отрисовки частей 
            // находится в левом верхнем углу элемента (0, 0), а не по центру. 
            // Изменим локальные координаты вершин квада, чтобы анимация не "разваливалась":
            Vector3 v0 = new Vector3(0, 0, 1.0f) * mat;
            Vector3 v1 = new Vector3(w, 0, 1.0f) * mat;
            Vector3 v2 = new Vector3(w, h, 1.0f) * mat;
            Vector3 v3 = new Vector3(0, h, 1.0f) * mat;

            batch.DrawDirectMatrix(region, v0.Xy, v1.Xy, v2.Xy, v3.Xy, new Color4(ColorOverride.R, ColorOverride.G, ColorOverride.B, finalAlpha));
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

                                // Добавлена безопасность .Trim() на случай лишних пробелов в XML
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
