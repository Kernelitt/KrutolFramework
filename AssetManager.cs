using SixLabors.Fonts.Unicode;
using System.Xml;
using System.Xml.Linq;

namespace KrutolFramework.Core
{
    public struct DefLoadResPath
    {
        public string Prefix;
        public string Directory;

        public DefLoadResPath(string prefix, string directory)
        {
            Prefix = prefix;
            Directory = directory;
        }
    }
    public class AssetGroup : IDisposable
    {
        public string Name { get; private set; }
        public DynamicTextureAtlas Atlas { get; private set; }

        private readonly Dictionary<string, FontRenderer> _fonts = new();
        private readonly Dictionary<string, ReanimDefinition> _animations = new();
        private readonly Dictionary<string, ParticleSystemDefinition> _particles = new();
        private readonly HashSet<string> _registeredTextures = new();

        public AssetGroup(string name, int atlasSize = 2048, int layersPerPage = 2)
        {
            Name = name;
            Atlas = new DynamicTextureAtlas(atlasSize, layersPerPage);
        }

        public TextureRegion LoadTexture(string assetName, string filePath, bool keepLocalPixels = false)
        {
            string key = $"{Name}/{assetName}";
            _registeredTextures.Add(key);
            return Atlas.RegisterTexture(key, filePath, keepLocalPixels);
        }

        public FontRenderer LoadFont(string fontName, string fontPathOrName, int fontSize)
        {
            string key = $"{fontName}_{fontSize}";
            if (_fonts.TryGetValue(key, out var existingFont)) return existingFont;

            var font = new FontRenderer(Atlas, fontPathOrName, fontSize);
            _fonts[key] = font;
            return font;
        }

        /// <summary>
        /// Загружает и парсит .reanim файл, привязывая его к текущей группе ресурсов.
        /// </summary>
        public ReanimDefinition LoadAnimation(string animName, string filePath)
        {
            if (_animations.TryGetValue(animName, out var existingAnim))
            {
                return existingAnim;
            }

            if (!File.Exists(filePath))
                throw new FileNotFoundException($".reanim файл не найден: {filePath}");

            // Парсим файл (код парсера находится ниже на Шаге 2)
            ReanimDefinition animDef = ReanimParser.ParseXml(filePath);

            _animations[animName] = animDef;
            Console.WriteLine($"[AssetManager] Анимация '{animName}' успешно загружена в группу '{Name}'. Кадров: {animDef.FrameCount}");
            return animDef;
        }

        public void DiscoverAndLoadTextures(string relativeFolder, bool keepLocalPixels = false)
        {
            // Вычисляем полный физический путь к общей корневой папке ассетов
            string rootFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath);
            // Вычисляем полный физический путь к целевой папке для сканирования
            string fullFolderPath = Path.Combine(rootFullPath, relativeFolder);

            if (!Directory.Exists(fullFolderPath))
            {
                Console.WriteLine($"[AssetManager] Предупреждение: Папка для сканирования не найдена: {fullFolderPath}");
                return;
            }

            // Рекурсивно собираем все файлы картинок в этой папке
            string[] allFiles = Directory.GetFiles(fullFolderPath, "*.png", SearchOption.AllDirectories);

            Console.WriteLine($"[AssetManager] Начинаем автосканирование папки '{relativeFolder}'. Найдено файлов: {allFiles.Length}");

            foreach (string fileFullPath in allFiles)
            {
                // ЖЕЛЕЗНЫЙ СПОСОБ: Получаем чистый относительный путь без использования Substring
                // На выходе будет, например: "reanim\CherryBomb_leaf3.png" или "images\button.png"
                string relativeFilePath = Path.GetRelativePath(rootFullPath, fileFullPath);

                // Генерируем ID по чистому относительному пути
                string assetId = AssetManager.GenerateResourceIdFromPath(relativeFilePath);

                try
                {
                    // Загружаем текстуру в атлас группы, используя сгенерированный ID
                    LoadTexture(assetId, fileFullPath, keepLocalPixels);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AssetManager] Ошибка загрузки файла {relativeFilePath}: {ex.Message}");
                }
            }

            Console.WriteLine($"[AssetManager] Группа '{Name}' успешно инициализирована. Всего текстур в атласе: {_registeredTextures.Count}");
        }


        // Добавьте этот метод внутрь класса AssetGroup в файле AssetManager.cs
        // Полностью замените метод LoadParticleSystem внутри AssetGroup (в файле AssetManager.cs)
        public ParticleSystemDefinition LoadParticleSystem(string sysName, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл системы частиц не найден: {filePath}");

            var sysDef = new ParticleSystemDefinition();
            var root = new System.Xml.XmlDocument();

            string rawXml = File.ReadAllText(filePath);
            string validXml = $"<particle_file>{rawXml}</particle_file>";
            root.LoadXml(validXml);

            XmlNode? doc = root.SelectSingleNode("particle_file");
            if (doc == null) return sysDef;

            System.Xml.XmlNodeList? emitterNodes = doc.SelectNodes("Emitter");
            foreach (System.Xml.XmlNode node in emitterNodes)
            {
                var eDef = new EmitterDefinition
                {
                    Name = node.SelectSingleNode("Name")?.InnerText?.Trim() ?? "Unknown",

                    SystemDuration = FloatTrack.Parse(node.SelectSingleNode("SystemDuration")?.InnerText),
                    SpawnMinActive = FloatTrack.Parse(node.SelectSingleNode("SpawnMinActive")?.InnerText),
                    SpawnMaxLaunched = FloatTrack.Parse(node.SelectSingleNode("SpawnMaxLaunched")?.InnerText),

                    // В XML Award.xml нет тегов EmitterBoxX/Y, поэтому они считаются по дефолту (0.0f)
                    EmitterBoxX = FloatTrack.Parse(node.SelectSingleNode("EmitterBoxX")?.InnerText),
                    EmitterBoxY = FloatTrack.Parse(node.SelectSingleNode("EmitterBoxY")?.InnerText),

                    ParticleDuration = FloatTrack.Parse(node.SelectSingleNode("ParticleDuration")?.InnerText),
                    LaunchSpeed = FloatTrack.Parse(node.SelectSingleNode("LaunchSpeed")?.InnerText),

                    // Цвета системы и частиц
                    ParticleRed = FloatTrack.Parse(node.SelectSingleNode("ParticleRed")?.InnerText),
                    ParticleGreen = FloatTrack.Parse(node.SelectSingleNode("ParticleGreen")?.InnerText),
                    ParticleBlue = FloatTrack.Parse(node.SelectSingleNode("ParticleBlue")?.InnerText),
                    ParticleAlpha = FloatTrack.Parse(node.SelectSingleNode("ParticleAlpha")?.InnerText),
                    ParticleBrightness = FloatTrack.Parse(node.SelectSingleNode("ParticleBrightness")?.InnerText),

                    // Вращение
                    ParticleSpinAngle = FloatTrack.Parse(node.SelectSingleNode("ParticleSpinAngle")?.InnerText),
                    ParticleSpinSpeed = FloatTrack.Parse(node.SelectSingleNode("ParticleSpinSpeed")?.InnerText),
                    ParticleScale = FloatTrack.Parse(node.SelectSingleNode("ParticleScale")?.InnerText),

                    ImageName = node.SelectSingleNode("Image")?.InnerText?.Trim() ?? "",

                    EmitterRadius = FloatTrack.Parse(node.SelectSingleNode("EmitterRadius")?.InnerText),
                    EmitterOffsetX = FloatTrack.Parse(node.SelectSingleNode("EmitterOffsetX")?.InnerText),
                    EmitterOffsetY = FloatTrack.Parse(node.SelectSingleNode("EmitterOffsetY")?.InnerText),
                    EmitterPath = FloatTrack.Parse(node.SelectSingleNode("EmitterPath")?.InnerText),
                    EmitterSkewX = FloatTrack.Parse(node.SelectSingleNode("EmitterSkewX")?.InnerText),
                    EmitterSkewY = FloatTrack.Parse(node.SelectSingleNode("EmitterSkewY")?.InnerText),
                    CrossFadeDuration = FloatTrack.Parse(node.SelectSingleNode("CrossFadeDuration")?.InnerText),
                    SpawnRate = FloatTrack.Parse(node.SelectSingleNode("SpawnRate")?.InnerText),
                    ParticleStretch = FloatTrack.Parse(node.SelectSingleNode("ParticleStretch")?.InnerText),
                    CollisionReflect = FloatTrack.Parse(node.SelectSingleNode("CollisionReflect")?.InnerText),
                    CollisionSpin = FloatTrack.Parse(node.SelectSingleNode("CollisionSpin")?.InnerText),
                    AnimationRate = FloatTrack.Parse(node.SelectSingleNode("AnimationRate")?.InnerText),

                };
                // Парсинг флагов эмиттера
                var emitterType = node.SelectSingleNode("EmitterType");
                if (emitterType != null)
                {
                    switch (emitterType.InnerText)
                    {
                        case "Box":
                            eDef.Type = EmitterType.Box;
                            break;
                        case "Circle":
                            eDef.Type = EmitterType.Circle;
                            break;
                        case "BoxPath":
                            eDef.Type = EmitterType.BoxPath;
                            break;
                        case "CirclePath":
                            eDef.Type = EmitterType.CirclePath;
                            break;
                        case "CircleEvenSpacing":
                            eDef.Type = EmitterType.CircleEvenSpacing;
                            break;
                    }
                }
                string? randSpinText = node.SelectSingleNode("RandomLaunchSpin")?.InnerText?.Trim();
                eDef.RandomLaunchSpin = randSpinText == "1" || randSpinText == "true";

                string? additiveText = node.SelectSingleNode("Additive")?.InnerText?.Trim();
                eDef.Additive = additiveText == "1" || additiveText == "true";

                string? fullScreenText = node.SelectSingleNode("FullScreen")?.InnerText?.Trim();
                eDef.FullScreen = fullScreenText == "1" || fullScreenText == "true";



                string? rowText = node.SelectSingleNode("ImageRow")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(rowText) && int.TryParse(rowText, out int row)) eDef.ImageRow = row;

                string? colText = node.SelectSingleNode("ImageCol")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(colText) && int.TryParse(colText, out int col)) eDef.ImageCol = col;

                string? framesText = node.SelectSingleNode("ImageFrames")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(framesText) && int.TryParse(framesText, out int frames)) eDef.ImageFrames = frames;

                eDef.ParticleRed.SetDefault(1.0f);
                eDef.ParticleGreen.SetDefault(1.0f);
                eDef.ParticleBlue.SetDefault(1.0f);
                eDef.ParticleAlpha.SetDefault(1.0f);
                eDef.ParticleBrightness.SetDefault(1.0f);
                eDef.ParticleSpinAngle.SetDefault(0.0f);
                eDef.ParticleSpinSpeed.SetDefault(0.0f);
                eDef.ParticleScale.SetDefault(1.0f);

                eDef.SystemRed.SetDefault(1.0f);
                eDef.SystemGreen.SetDefault(1.0f);
                eDef.SystemBlue.SetDefault(1.0f);
                eDef.SystemAlpha.SetDefault(1.0f);
                eDef.SystemBrightness.SetDefault(1.0f);

                eDef.ClipTop.SetDefault(0.0f);
                eDef.ClipBottom.SetDefault(0.0f);
                eDef.ClipLeft.SetDefault(0.0f);
                eDef.ClipRight.SetDefault(0.0f);

                eDef.EmitterRadius.SetDefault(0.0f);
                eDef.EmitterOffsetX.SetDefault(0.0f);
                eDef.EmitterOffsetY.SetDefault(0.0f);
                eDef.EmitterPath.SetDefault(0.0f);
                eDef.EmitterSkewX.SetDefault(0.0f);
                eDef.EmitterSkewY.SetDefault(0.0f);
                eDef.CrossFadeDuration.SetDefault(0.0f);
                eDef.SpawnRate.SetDefault(0.0f);
                eDef.ParticleStretch.SetDefault(1.0f); // По умолчанию растяжение равно 1.0f (без изменений)
                eDef.CollisionReflect.SetDefault(0.0f);
                eDef.CollisionSpin.SetDefault(0.0f);
                eDef.AnimationRate.SetDefault(0.0f);

                // ВАЖНО: Финализируем треки, подготавливая одиночные константы к интерполяции
                eDef.SystemDuration.Prepare();
                eDef.SpawnMinActive.Prepare();
                eDef.SpawnMaxLaunched.Prepare();
                eDef.EmitterBoxX.Prepare();
                eDef.EmitterBoxY.Prepare();
                eDef.ParticleDuration.Prepare();
                eDef.LaunchSpeed.Prepare();
                eDef.ParticleRed.Prepare();
                eDef.ParticleGreen.Prepare();
                eDef.ParticleBlue.Prepare();
                eDef.ParticleAlpha.Prepare();
                eDef.ParticleBrightness.Prepare();
                eDef.ParticleSpinAngle.Prepare();
                eDef.ParticleSpinSpeed.Prepare();
                eDef.ParticleScale.Prepare();

                eDef.SystemRed.Prepare();
                eDef.SystemGreen.Prepare();
                eDef.SystemBlue.Prepare();
                eDef.SystemAlpha.Prepare();
                eDef.SystemBrightness.Prepare();

                eDef.ClipTop.Prepare();
                eDef.ClipBottom.Prepare();
                eDef.ClipLeft.Prepare();
                eDef.ClipRight.Prepare();

                eDef.EmitterRadius.Prepare();
                eDef.EmitterOffsetX.Prepare();
                eDef.EmitterOffsetY.Prepare();
                eDef.EmitterPath.Prepare();
                eDef.EmitterSkewX.Prepare();
                eDef.EmitterSkewY.Prepare();
                eDef.CrossFadeDuration.Prepare();
                eDef.SpawnRate.Prepare();
                eDef.ParticleStretch.Prepare();
                eDef.CollisionReflect.Prepare();
                eDef.CollisionSpin.Prepare();
                eDef.AnimationRate.Prepare();

                // Читаем физические поля эмиттера <Field>
                System.Xml.XmlNodeList? fieldNodes = node.SelectNodes("Field");
                if (fieldNodes != null)
                {
                    foreach (System.Xml.XmlNode fNode in fieldNodes)
                    {
                        var fDef = new ParticleFieldDefinition
                        {
                            FieldType = fNode.SelectSingleNode("FieldType")?.InnerText?.Trim() ?? "",
                            X = FloatTrack.Parse(fNode.SelectSingleNode("X")?.InnerText),
                            Y = FloatTrack.Parse(fNode.SelectSingleNode("Y")?.InnerText)
                        };
                        eDef.Fields.Add(fDef);
                    }
                }

                System.Xml.XmlNodeList? sysFieldNodes = node.SelectNodes("SystemField");
                if (sysFieldNodes != null)
                {
                    foreach (System.Xml.XmlNode sfNode in sysFieldNodes)
                    {
                        var sfDef = new ParticleFieldDefinition
                        {
                            FieldType = sfNode.SelectSingleNode("FieldType")?.InnerText?.Trim() ?? "",
                            X = FloatTrack.Parse(sfNode.SelectSingleNode("X")?.InnerText),
                            Y = FloatTrack.Parse(sfNode.SelectSingleNode("Y")?.InnerText)
                        };
                        sfDef.X.Prepare();
                        sfDef.Y.Prepare();
                        eDef.SystemFields.Add(sfDef);
                    }
                }

                sysDef.Emitters.Add(eDef);
            }
            _particles[sysName] = sysDef;
            return sysDef;
        }

        public ReanimDefinition GetAnimation(string animName)
        {
            if (_animations.TryGetValue(animName, out var anim)) return anim;
            throw new KeyNotFoundException($"Анимация '{animName}' не найдена в группе '{Name}'.");
        }
        public ParticleSystemDefinition GetParticle(string particleName)
        {
            if (_particles.TryGetValue(particleName, out var particle)) return particle;
            throw new KeyNotFoundException($"Анимация '{particleName}' не найдена в группе '{Name}'.");
        }

        public FontRenderer GetFont(string fontName, int fontSize)
        {
            string key = $"{fontName}_{fontSize}";
            if (_fonts.TryGetValue(key, out var font)) return font;
            throw new KeyNotFoundException($"Шрифт '{fontName}' {fontSize}px не найден в группе '{Name}'.");
        }

        public void Dispose()
        {
            _fonts.Clear();
            _animations.Clear(); // Выгружаем тяжелые дефиниции треков и трансформаций из RAM
            _registeredTextures.Clear();

            if (Atlas != null)
            {
                Atlas.Dispose();
                Console.WriteLine($"[AssetManager] Группа '{Name}' полностью выгружена из GPU/VRAM и RAM.");
            }
        }
    }

    public static class AssetManager
    {
        private static readonly Dictionary<string, AssetGroup> _groups = new();
        public static string RootPath { get; set; } = "";
        public static AssetGroup Active;
        private static readonly DefLoadResPath[] _defLoadResPaths = new DefLoadResPath[]
        {
            new("IMAGE_REANIM_", "reanim\\"),
            new("IMAGE_REANIM_", "images\\"),
            new("IMAGE_", "particles\\"),
            new("IMAGE_", "") // Дефолтный корень
        };
        public static AssetGroup CreateGroup(string groupName, int atlasSize = 2048, int layersPerPage = 2)
        {
            if (_groups.TryGetValue(groupName, out var existingGroup))
            {
                existingGroup.Dispose();
            }

            var newGroup = new AssetGroup(groupName, atlasSize, layersPerPage);
            _groups[groupName] = newGroup;
            return newGroup;
        }

        public static AssetGroup GetGroup(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group)) return group;
            throw new KeyNotFoundException($"Группа ассетов '{groupName}' не найдена.");
        }

        public static void UnloadGroup(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Dispose();
                _groups.Remove(groupName);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public static string GetFullPath(string relativePath)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RootPath, relativePath);
        }



        public static string GenerateResourceIdFromPath(string relativeFilePath)
        {
            if (string.IsNullOrWhiteSpace(relativeFilePath)) return "IMAGE_UNKNOWN";

            // 1. Нормализуем слеши и переводим в нижний регистр для безопасного сравнения
            string normalizedPath = relativeFilePath.Replace('/', '\\').ToLower();

            // 2. Извлекаем чистую папку (например, "reanim") и чистое имя файла (например, "cherrybomb_leaf3")
            string directoryName = Path.GetDirectoryName(normalizedPath)?.Trim('\\') ?? "";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(normalizedPath) ?? "";

            // 3. Ищем подходящий префикс на основе имени папки
            string prefix = "IMAGE_"; // Дефолтный префикс по стандарту PopCap

            foreach (var resPath in _defLoadResPaths)
            {
                // Приводим папку из конфигурации к нижнему регистру и убираем слеши для точного сравнения
                string configDir = resPath.Directory.Replace('/', '\\').Trim('\\').ToLower();
                if (!string.IsNullOrEmpty(configDir) && directoryName == configDir)
                {
                    prefix = resPath.Prefix;
                    break;
                }
            }

            // 4. Очищаем имя файла от пробелов, тире и спецсимволов, заменяя их на подчёркивание
            string cleanFileName = fileNameWithoutExt
                .Replace('-', '_')
                .Replace(' ', '_');

            // 5. Собираем финальный ID в UPPERCASE без какого-либо ручного вырезания подстрок
            string finalResourceId = (prefix + cleanFileName).ToUpper();
            return finalResourceId;
        }

        public static TextureRegion? GetTexture(string assetName) => Active?.Atlas.GetRegion($"{Active.Name}/{assetName}");
        public static ParticleSystemDefinition? GetParticle(string particleName) => Active?.GetParticle(particleName);
        public static ReanimDefinition? GetAnimation(string animName) => Active?.GetAnimation(animName);
        public static FontRenderer? GetFont(string fontName, int fontSize) => Active?.GetFont(fontName, fontSize);
        public static void UnloadAll()
        {
            foreach (var group in _groups.Values) group.Dispose();
            _groups.Clear();
            GC.Collect();
        }
    }
}
