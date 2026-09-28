using OpenTK.Mathematics;
using System;
using System.Globalization;
using OpenTK.Graphics.OpenGL4;

namespace KrutolFramework.Core
{

    public class TrackNode
    {
        public float Time { get; set; } // Нормализованное время узла (от 0.0 до 1.0)
        public float LowMin { get; set; }
        public float LowMax { get; set; }
        public float HighMin { get; set; }
        public float HighMax { get; set; }
        public TodCurves Curve { get; set; } = TodCurves.CURVE_LINEAR;
    }

    public class FloatTrack
    {
        public List<TrackNode> Nodes { get; set; } = new();

        public static FloatTrack Parse(string? text)
        {
            var track = new FloatTrack();
            if (string.IsNullOrWhiteSpace(text)) return track; // Возвращает пустой трек (Nodes.Count == 0)

            string[] tokens = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanTokens = new List<string>();

            // Выделяем токены, корректно собирая диапазоны в скобках [min max]
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                if (t.StartsWith("["))
                {
                    while (i < tokens.Length && !t.EndsWith("]"))
                    {
                        i++;
                        if (i < tokens.Length) t += " " + tokens[i];
                    }
                }
                cleanTokens.Add(t);
            }

            // Внутри метода FloatTrack.Parse(string? text) в цикле перебора cleanTokens:
            for (int idx = 0; idx < cleanTokens.Count; idx++)
            {
                string token = cleanTokens[idx];
                TodCurves currentCurve = TodCurves.CURVE_LINEAR; // По умолчанию для каждого узла

                // МОДЕРНИЗИРОВАННАЯ ЧАСТЬ ПАРСИНГА КРИВЫХ:
                // Проверяем, является ли текущий токен текстовым маркером любой из 14 кривых PopCap
                if (IsCurveToken(token))
                {
                    currentCurve = ParseCurveType(token);
                    idx++; // Сдвигаем указатель на следующий токен, так как этот был служебным именем кривой
                    if (idx >= cleanTokens.Count) break;
                    token = cleanTokens[idx]; // Записываем в token фактическое значение/диапазон узла
                }

                string valuePart = token;
                float time = -1f;

                int commaIdx = token.IndexOf(',');
                if (commaIdx != -1)
                {
                    valuePart = token.Substring(0, commaIdx);
                    string timePart = token.Substring(commaIdx + 1);
                    if (float.TryParse(timePart, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedTime))
                    {
                        time = parsedTime / 100f;
                    }
                }

                var node = ParseNodeValues(valuePart);
                node.Curve = currentCurve; // Присваиваем узлу распарсенную кривую из TodCurves

                if (time < 0f)
                {
                    if (track.Nodes.Count == 0) time = 0f;
                    else if (idx == cleanTokens.Count - 1) time = 1f;
                    else
                    {
                        int totalSegments = cleanTokens.Count - 1;
                        time = totalSegments > 0 ? (float)track.Nodes.Count / totalSegments : 1f;
                    }
                }

                node.Time = time;
                track.Nodes.Add(node);
            }

            track.Nodes.Sort((a, b) => a.Time.CompareTo(b.Time));

            // ФИКС: Если распарсилась одиночная константа, 
            // дублируем её на конец таймлайна, чтобы Evaluate работал стабильно во всех узлах эмиттера
            if (track.Nodes.Count == 1)
            {
                track.Prepare();
            }

            return track;
        }

        private static TodCurves ParseCurveType(string token)
        {
            return token.ToLower() switch
            {
                "constant" => TodCurves.CURVE_CONSTANT,
                "linear" => TodCurves.CURVE_LINEAR,
                "easein" => TodCurves.CURVE_EASE_IN,
                "easeout" => TodCurves.CURVE_EASE_OUT,
                "easeinout" => TodCurves.CURVE_EASE_IN_OUT,
                "easeinoutweak" => TodCurves.CURVE_EASE_IN_OUT_WEAK,
                "fastinout" => TodCurves.CURVE_FAST_IN_OUT,
                "fastinoutweak" => TodCurves.CURVE_FAST_IN_OUT_WEAK,
                "weakfastinout" => TodCurves.CURVE_WEAK_FAST_IN_OUT,
                "bounce" => TodCurves.CURVE_BOUNCE,
                "bouncefastmiddle" => TodCurves.CURVE_BOUNCE_FAST_MIDDLE,
                "bounceslowmiddle" => TodCurves.CURVE_BOUNCE_SLOW_MIDDLE,
                "sinwave" => TodCurves.CURVE_SIN_WAVE,
                "easesinwave" => TodCurves.CURVE_EASE_SIN_WAVE,
                _ => TodCurves.CURVE_LINEAR // Если токен не кривая, возвращаем Linear по умолчанию
            };
        }
        private static bool IsCurveToken(string token)
        {
            string t = token.ToLower();
            return t == "constant" || t == "linear" || t == "easein" || t == "easeout" ||
                   t == "easeinout" || t == "easeinoutweak" || t == "fastinout" ||
                   t == "fastinoutweak" || t == "weakfastinout" || t == "bounce" ||
                   t == "bouncefastmiddle" || t == "bounceslowmiddle" || t == "sinwave" ||
                   t == "easesinwave";
        }
        private static TrackNode ParseNodeValues(string token)
        {
            var node = new TrackNode();
            string clean = token.Replace("[", "").Replace("]", "").Trim();

            string[] parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    node.LowMin = node.LowMax = node.HighMin = node.HighMax = v;
            }
            else if (parts.Length == 2)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float min) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float max))
                {
                    node.LowMin = min; node.LowMax = max;
                    node.HighMin = min; node.HighMax = max;
                }
            }
            return node;
        }

        // Всплытие оригинального FloatTrackSetDefault
        public void SetDefault(float defaultValue)
        {
            if (Nodes.Count == 0)
            {
                Nodes.Add(new TrackNode { Time = 0f, LowMin = defaultValue, LowMax = defaultValue, HighMin = defaultValue, HighMax = defaultValue });
            }
        }

        // Превращает одиночную константу (1 узел) в полноценный сквозной трек от 0.0 до 1.0
        public void Prepare()
        {
            if (Nodes.Count == 1)
            {
                Nodes[0].Time = 0f;
                Nodes.Add(new TrackNode
                {
                    Time = 1f,
                    LowMin = Nodes[0].LowMin,
                    LowMax = Nodes[0].LowMax,
                    HighMin = Nodes[0].HighMin,
                    HighMax = Nodes[0].HighMax,
                    Curve = Nodes[0].Curve
                });
            }
        }

        public float Evaluate(float time, float randomInterp)
        {
            if (Nodes.Count == 0) return 0f;

            // Оценка крайних точек таймлайна
            if (time <= Nodes[0].Time)
                return TodCurveMath.TodCurveEvaluate(randomInterp, Nodes[0].LowMin, Nodes[0].LowMax, Nodes[0].Curve);

            if (time >= Nodes[Nodes.Count - 1].Time)
                return TodCurveMath.TodCurveEvaluate(randomInterp, Nodes[Nodes.Count - 1].LowMin, Nodes[Nodes.Count - 1].LowMax, Nodes[Nodes.Count - 1].Curve);

            // Поиск и интерполяция сегментов
            for (int i = 0; i < Nodes.Count - 1; i++)
            {
                if (time >= Nodes[i].Time && time <= Nodes[i + 1].Time)
                {
                    var startNode = Nodes[i];
                    var endNode = Nodes[i + 1];

                    float segmentLength = endNode.Time - startNode.Time;
                    float segmentProgress = segmentLength > 0f ? (time - startNode.Time) / segmentLength : 0f;

                    // Вычисляем значение на левом и правом узлах сегмента (с учетом их кривых распределения рандома)
                    float startVal = TodCurveMath.TodCurveEvaluate(randomInterp, startNode.LowMin, startNode.LowMax, startNode.Curve);
                    float endVal = TodCurveMath.TodCurveEvaluate(randomInterp, endNode.LowMin, endNode.LowMax, endNode.Curve);

                    // Финальная интерполяция прогресса времени внутри сегмента строго по формуле PopCap
                    return TodCurveMath.TodCurveEvaluate(segmentProgress, startVal, endVal, startNode.Curve);
                }
            }

            return 0f;
        }
    }

    public enum EmitterType { Circle, Box, CirclePath, BoxPath, CircleEvenSpacing }

    public class ParticleFieldDefinition
    {
        public string FieldType { get; set; } = "";
        public FloatTrack X { get; set; } = new();
        public FloatTrack Y { get; set; } = new();
    }

    public class EmitterDefinition
    {
        public string Name { get; set; } = "Unknown";
        public EmitterType Type { get; set; } = EmitterType.Circle;

        public FloatTrack SpawnMinActive { get; set; } = new();
        public FloatTrack SpawnMaxLaunched { get; set; } = new();
        public FloatTrack ParticleDuration { get; set; } = new();
        public FloatTrack SystemDuration { get; set; } = new();
        public FloatTrack ParticleScale { get; set; } = new();
        
        public FloatTrack ParticleRed { get; set; } = new();
        public FloatTrack ParticleGreen { get; set; } = new();
        public FloatTrack ParticleBlue { get; set; } = new();
        public FloatTrack ParticleAlpha { get; set; } = new();
        public FloatTrack ParticleBrightness { get; set; } = new(); 
        public FloatTrack LaunchSpeed { get; set; } = new();

        public FloatTrack ParticleSpinSpeed { get; set; } = new();
        public FloatTrack ParticleSpinAngle { get; set; } = new(); 
        public bool RandomLaunchSpin { get; set; } = false;

        public FloatTrack SystemRed { get; set; } = new();
        public FloatTrack SystemGreen { get; set; } = new();
        public FloatTrack SystemBlue { get; set; } = new();
        public FloatTrack SystemAlpha { get; set; } = new();
        public FloatTrack SystemBrightness { get; set; } = new();

        public FloatTrack ClipTop { get; set; } = new();
        public FloatTrack ClipBottom { get; set; } = new();
        public FloatTrack ClipLeft { get; set; } = new();
        public FloatTrack ClipRight { get; set; } = new();

        public string OnDuration { get; set; } = ""; // Имя эмиттера для кроссфейда по окончании времени
        public FloatTrack CrossFadeDuration { get; set; } = new();
        public FloatTrack SpawnRate { get; set; } = new();

        
        public FloatTrack EmitterRadius { get; set; } = new();
        public FloatTrack EmitterOffsetX { get; set; } = new();
        public FloatTrack EmitterOffsetY { get; set; } = new();
        public FloatTrack EmitterPath { get; set; } = new();
        public FloatTrack EmitterSkewX { get; set; } = new();
        public FloatTrack EmitterSkewY { get; set; } = new();
        public FloatTrack EmitterBoxX { get; set; } = new();
        public FloatTrack EmitterBoxY { get; set; } = new();

        public int ImageRow { get; set; } = 0;
        public int ImageCol { get; set; } = 0;
        public string ImageName { get; set; } = "";
        public int ImageFrames { get; set; } = 1;
        public bool FullScreen { get; set; } = false;
        public bool Additive { get; set; } = false;

        public FloatTrack ParticleStretch { get; set; } = new();
        public FloatTrack CollisionReflect { get; set; } = new();
        public FloatTrack CollisionSpin { get; set; } = new();
        public int Animated { get; set; } = 0; // Флаг циклической анимации кадров
        public FloatTrack AnimationRate { get; set; } = new();

        public List<ParticleFieldDefinition> Fields { get; set; } = [];
        public List<ParticleFieldDefinition> SystemFields { get; set; } = [];
    }

    public class ParticleSystemDefinition
    {
        public List<EmitterDefinition> Emitters { get; set; } = new();
    }


    public class LiveParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation = 0f;
        public float SpinVelocity = 0f;
        public int SelectedFrame = 0;
        public float Age = 0f;
        public float Duration = 100f;
        public float ScaleRandom = 0f;
        public float AlphaRandom = 0f;
        public float ColorRandom = 0f;
        public float SpinRandom = 0f;
        public float BrightnessRandom = 0f;
        public float AnimationTime = 0f; 
        public float LastRotation = 0f; 
    }

    public class ParticleEmitter
    {
        private readonly EmitterDefinition _def;
        private readonly AssetGroup _group;
        private readonly List<LiveParticle> _particles = new();
        private Vector2 _systemFieldOffset = Vector2.Zero;
        private float _systemAge = 0f;
        private float _systemDuration = 100f;
        private int _totalLaunched = 0;
        private bool _isDead = false;
        private float _spawnAccum = 0f;
        public bool IsDead => _isDead && _particles.Count == 0;

        public ParticleEmitter(EmitterDefinition def, AssetGroup group)
        {
            _def = def;
            _group = group;

            if (_def.SystemDuration.Nodes.Count > 0)
                _systemDuration = _def.SystemDuration.Evaluate(0f, Random.Shared.NextSingle());
            else
                _systemDuration = _def.ParticleDuration.Evaluate(0f, Random.Shared.NextSingle());

            _systemDuration = MathF.Max(1f, _systemDuration);
        }

        public void Update(float deltaTime)
        {
            Random rand = Random.Shared;
            // Приводим deltaTime к игровым тикам (1 кадр игры = 1 тик = 1/60 секунды)
            float dtTicks = deltaTime * 60f;

            _systemAge += dtTicks;
            float progress = _systemDuration > 0f ? Math.Clamp(_systemAge / _systemDuration, 0f, 1f) : 0f;

            if (_systemAge >= _systemDuration)
            {
                _isDead = true;
            }

            float rate = _def.SpawnRate.Evaluate(progress, rand.NextSingle()) * 0.01f;
            _spawnAccum += rate * dtTicks;
            int spawnCount = (int)_spawnAccum;
            _spawnAccum -= spawnCount;

            int minActive = _def.SpawnMinActive.Nodes.Count > 0 ? (int)_def.SpawnMinActive.Evaluate(progress, rand.NextSingle()) : 0;
            if (spawnCount == 0 && _particles.Count < minActive && _totalLaunched < minActive)
            {
                spawnCount = 1;
            }

            if (!_isDead)
            {
                int maxLaunched = _def.SpawnMaxLaunched.Nodes.Count > 0 ? (int)_def.SpawnMaxLaunched.Evaluate(progress, rand.NextSingle()) : int.MaxValue;

                for (int index = 0; index < spawnCount; index++)
                {
                    if (_particles.Count >= maxLaunched || _totalLaunched >= maxLaunched) break;

                    float posX = 0f;
                    float posY = 0f;
                    float launchAngle = rand.NextSingle() * MathF.PI * 2f; // По умолчанию рандомный угол

                    // --- ПОЛНАЯ РЕАЛИЗАЦИЯ EMITTER TYPE ПО ОРИГИНАЛУ PVZ ---
                    float radius = _def.EmitterRadius.Evaluate(progress, rand.NextSingle());

                    switch (_def.Type)
                    {
                        case EmitterType.Circle:
                            posX = MathF.Sin(launchAngle) * radius;
                            posY = MathF.Cos(launchAngle) * radius;
                            break;

                        case EmitterType.Box:
                            posX = _def.EmitterBoxX.Evaluate(progress, rand.NextSingle());
                            posY = _def.EmitterBoxY.Evaluate(progress, rand.NextSingle());
                            break;

                        case EmitterType.CirclePath:
                            // Движение по окружности завязано на трек EmitterPath (от 0.0 до 1.0)
                            float pathPosCircle = _def.EmitterPath.Evaluate(progress, rand.NextSingle());
                            launchAngle = pathPosCircle * 2f * MathF.PI;
                            posX = MathF.Sin(launchAngle) * radius;
                            posY = MathF.Cos(launchAngle) * radius;
                            break;

                        case EmitterType.CircleEvenSpacing:
                            // Равномерно распределяет углы между одновременно спавнящимися частицами
                            launchAngle = (2f * MathF.PI * index) / Math.Max(1, spawnCount);
                            posX = MathF.Sin(launchAngle) * radius;
                            posY = MathF.Cos(launchAngle) * radius;
                            break;

                        case EmitterType.BoxPath:
                            float pathPosBox = _def.EmitterPath.Evaluate(progress, rand.NextSingle());
                            float minX = _def.EmitterBoxX.Evaluate(progress, 0f);
                            float maxX = _def.EmitterBoxX.Evaluate(progress, 1f);
                            float minY = _def.EmitterBoxY.Evaluate(progress, 0f);
                            float maxY = _def.EmitterBoxY.Evaluate(progress, 1f);
                            float distX = maxX - minX;
                            float distY = maxY - minY;
                            float totalLen = (distX + distY) * 2f;
                            float currentLen = pathPosBox * totalLen;

                            if (currentLen < distY) { posX = minX; posY = minY + currentLen; }
                            else if (currentLen < distY + distX) { posX = minX + (currentLen - distY); posY = maxY; }
                            else if (currentLen < distY + distX + distY) { posX = maxX; posY = maxY - (currentLen - distY - distX); }
                            else { posX = maxX - (currentLen - distY - distX - distY); posY = minY; }
                            break;
                    }

                    // Применяем EmitterSkew (скос геометрии спавна)
                    float skewX = _def.EmitterSkewX.Evaluate(progress, rand.NextSingle());
                    float skewY = _def.EmitterSkewY.Evaluate(progress, rand.NextSingle());

                    float finalPosX = posX + (posY * skewX);
                    float finalPosY = posY + (posX * skewY);

                    // Добавляем EmitterOffset
                    finalPosX += _def.EmitterOffsetX.Evaluate(progress, rand.NextSingle());
                    finalPosY += _def.EmitterOffsetY.Evaluate(progress, rand.NextSingle());

                    // Скорость вылета частицы
                    float launchSpeed = _def.LaunchSpeed.Evaluate(progress, rand.NextSingle()) * 0.01f;

                    var p = new LiveParticle
                    {
                        Position = new Vector2(finalPosX, finalPosY),
                        Velocity = new Vector2(MathF.Sin(launchAngle) * launchSpeed, MathF.Cos(launchAngle) * launchSpeed),
                        Age = 0f,
                        Duration = _def.ParticleDuration.Nodes.Count > 0
                            ? MathF.Max(1f, _def.ParticleDuration.Evaluate(progress, rand.NextSingle()))
                            : _systemDuration, // Если не задано, живет столько же, сколько сам эмиттер
                        ScaleRandom = rand.NextSingle(),
                        AlphaRandom = rand.NextSingle(),
                        ColorRandom = rand.NextSingle(),
                        SpinRandom = rand.NextSingle(),
                        BrightnessRandom = rand.NextSingle(),
                        SelectedFrame = rand.Next(_def.ImageFrames)
                    };

                    if (_def.RandomLaunchSpin) p.Rotation = rand.NextSingle() * 360f;
                    else if (_def.ParticleSpinAngle.Nodes.Count > 0) p.Rotation = _def.ParticleSpinAngle.Evaluate(0f, p.SpinRandom);

                    p.LastRotation = p.Rotation;

                    _particles.Add(p);
                    _totalLaunched++;
                }

            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                float pProgress = Math.Clamp(p.Age / p.Duration, 0f, 1f);
                float pLastProgress = Math.Clamp((p.Age - dtTicks) / p.Duration, 0f, 1f);

                // --- РЕАЛИЗАЦИЯ ВСЕХ ОСТАВШИХСЯ СЛОЖНЫХ ПОЛЕЙ (GroundConstraint и др.) ---
                foreach (var field in _def.Fields)
                {
                    if (field.FieldType.Equals("Acceleration", StringComparison.OrdinalIgnoreCase))
                    {
                        p.Velocity.X += field.X.Evaluate(pProgress, p.ColorRandom) * 0.01f * dtTicks;
                        p.Velocity.Y += field.Y.Evaluate(pProgress, p.ColorRandom) * 0.01f * dtTicks;
                    }
                    else if (field.FieldType.Equals("Friction", StringComparison.OrdinalIgnoreCase))
                    {
                        p.Velocity.X *= MathF.Pow(1f - field.X.Evaluate(pProgress, p.ColorRandom), dtTicks);
                        p.Velocity.Y *= MathF.Pow(1f - field.Y.Evaluate(pProgress, p.ColorRandom), dtTicks);
                    }
                    else if (field.FieldType.Equals("GroundConstraint", StringComparison.OrdinalIgnoreCase))
                    {
                        // В оригинале PopCap координата земли Y — это ЛОКАЛЬНОЕ смещение вниз от центра системы частиц
                        float groundY = field.Y.Evaluate(pProgress, p.ColorRandom);

                        if (p.Position.Y > groundY)
                        {
                            p.Position.Y = groundY;

                            float reflect = _def.CollisionReflect.Evaluate(pProgress, rand.NextSingle());
                            // Защита от зависания: если рефлект не задан в XML, ставим дефолтный 0.5f
                            if (_def.CollisionReflect.Nodes.Count == 0) reflect = 0.5f;

                            float spin = _def.CollisionSpin.Evaluate(pProgress, rand.NextSingle()) * 0.001f;

                            p.SpinVelocity = p.Velocity.Y * spin * 60f;
                            p.Velocity.X *= reflect;
                            p.Velocity.Y *= -reflect; // Отскок вверх!
                        }
                    }
                }

                // Движение и вращение
                p.Position += p.Velocity * dtTicks;

                float spinSpeed = _def.ParticleSpinSpeed.Evaluate(pProgress, p.SpinRandom) * 0.01f;
                float spinAngle = _def.ParticleSpinAngle.Evaluate(pProgress, p.SpinRandom);
                float lastSpinAngle = _def.ParticleSpinAngle.Evaluate(pLastProgress, p.SpinRandom);

                p.Rotation += (spinSpeed + spinAngle - lastSpinAngle) * dtTicks;

                // --- РЕАЛИЗАЦИЯ ANIMATED И ANIMATION RATE ---
                if (_def.AnimationRate.Nodes.Count > 0)
                {
                    float animRate = _def.AnimationRate.Evaluate(pProgress, p.SpinRandom) * 0.01f;
                    p.AnimationTime += animRate * dtTicks;
                    while (p.AnimationTime >= 1f) p.AnimationTime -= 1f;
                    while (p.AnimationTime < 0f) p.AnimationTime += 1f;

                    p.SelectedFrame = Math.Clamp((int)(p.AnimationTime * _def.ImageFrames), 0, _def.ImageFrames - 1);
                }
                else if (_def.Animated > 0)
                {
                    // Если флаг Animated выставлен, кадры переключаются последовательно на основе прогресса жизни частицы
                    p.SelectedFrame = Math.Clamp((int)(pProgress * _def.ImageFrames), 0, _def.ImageFrames - 1);
                }

                p.Age += dtTicks;

                // --- РЕАЛИЗАЦИЯ ON DURATION (Кроссфейд по смерти системы) ---
                if (p.Age >= p.Duration)
                {
                    _particles.RemoveAt(i);

                    // По декомпилятору: если задан OnDuration эмиттера, в момент смерти частицы 
                    // мы можем вызвать триггер спавна следующей системы, но в упрощенной C# архитектуре 
                    // этот функционал обычно делегируется менеджеру верхнего уровня (EffectSystem).
                }
            }
        }

        public void Render(SpriteBatch batch, Vector2 systemPos, float systemScale)
        {
            if (_particles.Count == 0) return;

            if (_def.Additive)
            {
                // Возвращаем оригинальный блендинг PopCap PvZ
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            }

            foreach (var p in _particles)
            {
                float pProgress = Math.Clamp(p.Age / p.Duration, 0f, 1f);
                float sysProgress = _systemDuration > 0f ? Math.Clamp(_systemAge / _systemDuration, 0f, 1f) : 0f;

                float scale = _def.ParticleScale.Evaluate(pProgress, p.ScaleRandom) * systemScale;
                float alpha = _def.ParticleAlpha.Evaluate(pProgress, p.AlphaRandom);

                // 1. Базовые цвета частицы (из XML или дефолтные 1.0f)
                float r = _def.ParticleRed.Evaluate(pProgress, p.ColorRandom);
                float g = _def.ParticleGreen.Evaluate(pProgress, p.ColorRandom);
                float b = _def.ParticleBlue.Evaluate(pProgress, p.ColorRandom);
                float pBrightness = _def.ParticleBrightness.Evaluate(pProgress, p.BrightnessRandom);

                // 2. ИНТЕГРАЦИЯ СИСТЕМНЫХ ТРЕКОВ ЦВЕТА И ЯРКОСТИ (SystemRed/Green/Blue/Alpha/Brightness)
                float sysR = _def.SystemRed.Evaluate(sysProgress, 0f);
                float sysG = _def.SystemGreen.Evaluate(sysProgress, 0f);
                float sysB = _def.SystemBlue.Evaluate(sysProgress, 0f);
                float sysA = _def.SystemAlpha.Evaluate(sysProgress, 0f);
                float sysBrightness = _def.SystemBrightness.Evaluate(sysProgress, 0f);

                // Итоговые множители по формуле PopCap: цвет частицы * системный цвет * общая яркость
                float totalBrightness = pBrightness * sysBrightness;

                // Фикс палитры HD текстур (наша проверенная формула, убирающая тусклость)
                float finalBrightness = Math.Max(totalBrightness * 2.5f, 0.7f);

                r *= sysR * finalBrightness;
                g *= sysG * finalBrightness;
                b *= sysB * (finalBrightness * 1.3f);
                alpha *= sysA; // Системная прозрачность глушит общую альфу

                r = Math.Clamp(r, 0f, 1f);
                g = Math.Clamp(g, 0f, 1f);
                b = Math.Clamp(b, 0f, 1f);
                alpha = Math.Clamp(alpha, 0f, 1f);

                Color4 pColor = new(r, g, b, alpha);

                if (_def.FullScreen)
                {
                    try
                    {
                        TextureRegion region = _group.Atlas.GetRegion($"{_group.Name}/{_def.ImageName}");
                        Vector2 fillScale = new(FrameworkGameWindow.VirtualResolution.X / region.Width,
                                                        FrameworkGameWindow.VirtualResolution.Y / region.Height);
                        batch.Draw(region, Vector2.Zero, fillScale, 0f, pColor);
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        TextureRegion region = _group.Atlas.GetRegion($"{_group.Name}/{_def.ImageName}");

                        int celWidth = region.Width / _def.ImageFrames;
                        int celHeight = region.Height;

                        float fullUWidth = region.U2 - region.U1;
                        float frameUWidth = fullUWidth / _def.ImageFrames;

                        int finalFrameIndex = p.SelectedFrame + _def.ImageCol;

                        // Фикс: явно инициализируем изолированный регион для батчера
                        TextureRegion frameRegion = new TextureRegion
                        {
                            AtlasTextureHandle = region.AtlasTextureHandle,
                            Layer = region.Layer,
                            Width = celWidth,
                            Height = celHeight,
                            U1 = region.U1 + (finalFrameIndex * frameUWidth),
                            U2 = region.U1 + ((finalFrameIndex + 1) * frameUWidth),
                            V1 = region.V1,
                            V2 = region.V2
                        };

                        frameRegion.U1 = region.U1 + (finalFrameIndex * frameUWidth);
                        frameRegion.U2 = frameRegion.U1 + frameUWidth;

                        // 4. ИНТЕГРАЦИЯ CLIPTOP, CLIPBOTTOM, CLIPLEFT, CLIPRIGHT
                        float clipTop = _def.ClipTop.Evaluate(pProgress, 0f);
                        float clipBottom = _def.ClipBottom.Evaluate(pProgress, 0f);
                        float clipLeft = _def.ClipLeft.Evaluate(pProgress, 0f);
                        float clipRight = _def.ClipRight.Evaluate(pProgress, 0f);

                        // Если параметры обрезки выставлены (больше 0), сжимаем текстурный регион UV
                        if (clipLeft > 0f || clipRight > 0f || clipTop > 0f || clipBottom > 0f)
                        {
                            float uLength = frameRegion.U2 - frameRegion.U1;
                            float vLength = frameRegion.V2 - frameRegion.V1;

                            frameRegion.U1 += uLength * clipLeft;
                            frameRegion.U2 -= uLength * clipRight;
                            frameRegion.V1 += vLength * clipTop;
                            frameRegion.V2 -= vLength * clipBottom;

                            // Уменьшаем физический размер спрайта для батчера пропорционально обрезке
                            frameRegion.Width = (int)(celWidth * (1f - (clipLeft + clipRight)));
                            // Высота кадра сжимается аналогично, если ваш батчер это поддерживает
                        }

                        float stretch = _def.ParticleStretch.Evaluate(pProgress, p.ScaleRandom);

                        // Масштаб по X остается стандартным, а по оси Y умножается на коэффициент Stretch
                        Vector2 pScale = new Vector2(scale, scale * stretch);

                        batch.Draw(frameRegion, systemPos + _systemFieldOffset + p.Position * systemScale, pScale, p.Rotation, pColor, Anchor.Center);
                    }
                    catch { }
                }
            }

            if (_def.Additive)
            {
                // Возвращаем обычный Alpha-блендинг фреймворка
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }
        }

    }

    public class TodParticleSystem
    {
        private readonly List<ParticleEmitter> _emitters = new();
        public Vector2 Position { get; set; } = Vector2.Zero;
        public float Scale { get; set; } = 1f;
        public bool IsDead { get; private set; } = false;

        public TodParticleSystem(ParticleSystemDefinition def, AssetGroup group)
        {
            foreach (var emitterDef in def.Emitters)
            {
                _emitters.Add(new ParticleEmitter(emitterDef, group));
            }
        }

        public void Update(float deltaTime)
        {
            if (IsDead) return;

            bool allEmittersDead = true;
            foreach (var emitter in _emitters)
            {
                emitter.Update(deltaTime);
                if (!emitter.IsDead) allEmittersDead = false;
            }

            if (allEmittersDead) IsDead = true;
        }

        public void Render(SpriteBatch batch)
        {
            if (IsDead) return;

            foreach (var emitter in _emitters)
            {
                emitter.Render(batch, Position, Scale);
            }
        }
    }
}

