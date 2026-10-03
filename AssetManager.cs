using OpenTK.Audio.OpenAL;
using SixLabors.Fonts.Unicode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

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

        private readonly Dictionary<string, FontRenderer> _fonts = [];
        private readonly Dictionary<string, ReanimDefinition> _animations = [];
        private readonly Dictionary<string, ParticleSystemDefinition> _particles = [];
        private readonly Dictionary<string, int> _audioBuffers = [];
        private readonly HashSet<string> _registeredTextures = [];
        private readonly HashSet<string> _registeredParticles = [];
        private readonly HashSet<string> _registeredAnimations = new();

        public AssetGroup(string name, int atlasSize = 2048, int layersPerPage = 2)
        {
            Name = name;
            Atlas = new DynamicTextureAtlas(atlasSize, layersPerPage);
        }

        // Вспомогательный метод для получения потока данных (диск или Zip)
        private Stream OpenAssetStream(string filePath)
        {
            if (AssetManager.IsZipMode)
            {
                return AssetManager.OpenZipStream(filePath);
            }

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл ресурса не найден: {filePath}");

            return File.OpenRead(filePath);
        }

        public TextureRegion LoadTexture(string assetName, string filePath, bool keepLocalPixels = false)
        {
            string cleanAssetName = assetName.Replace(" ", "");
            string key = $"{Name}/{cleanAssetName}";
            _registeredTextures.Add(key);

            // Если ваш DynamicTextureAtlas принимает только filePath, его нужно будет перегрузить на Stream/byte[].
            // Для совместимости: если режим Zip, мы можем временно извлечь или (если атлас поддерживает) передать поток.
            // Предполагаем, что Atlas теперь поддерживает загрузку по пути или вы адаптируете его под стримы.
            if (AssetManager.IsZipMode)
            {
                using var stream = OpenAssetStream(filePath);
                // ВАЖНО: Если ваш DynamicTextureAtlas не имеет перегрузки под Stream, 
                // вы можете временно сохранить файл в Temp или дописать в класс атласа прием Stream/Image.
                return Atlas.RegisterTexture(key, stream, keepLocalPixels);
            }

            return Atlas.RegisterTexture(key, filePath, keepLocalPixels);
        }

        public FontRenderer LoadFont(string fontName, string fontPathOrName, int fontSize)
        {
            string key = $"{fontName}{fontSize}";
            if (_fonts.TryGetValue(key, out var existingFont)) return existingFont;

            FontRenderer font;

            if (AssetManager.IsZipMode)
            {
                // Проверяем, существует ли файл в кэше ZIP-архива
                // Корректно стыкуем RootPath и путь к шрифту под стандарты ZIP
                string fullZipPath = Path.Combine(AssetManager.RootPath, fontPathOrName).Replace('/', '\\');

                // Передаем логику проверки в метод архива
                IEnumerable<string> foundFiles = AssetManager.EnumerateZipFiles("", [Path.GetFileName(fontPathOrName).ToLower()]);

                if (foundFiles.Any())
                {
                    // Читаем шрифт из архива в память в виде массива байт
                    using var stream = OpenAssetStream(fontPathOrName);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    byte[] fontData = ms.ToArray();

                    font = new FontRenderer(Atlas, fontPathOrName, fontSize, fontData);
                }
                else
                {
                    // Если в архиве файла нет, пробуем подгрузить как системный (например, "Arial")
                    font = new FontRenderer(Atlas, fontPathOrName, fontSize, null);
                }
            }
            else
            {
                // Обычный режим разработки (чтение файлов с диска)
                string fullDiskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath, fontPathOrName);

                if (File.Exists(fullDiskPath))
                {
                    byte[] fontData = File.ReadAllBytes(fullDiskPath);
                    font = new FontRenderer(Atlas, fontPathOrName, fontSize, fontData);
                }
                else
                {
                    // Если файла на диске нет, пробуем подгрузить системный
                    font = new FontRenderer(Atlas, fontPathOrName, fontSize, null);
                }
            }

            _fonts[key] = font;
            return font;
        }


        public int LoadAudio(string assetName, string filePath)
        {
            string key = $"{Name}/{assetName}";
            if (_audioBuffers.TryGetValue(key, out var existingBuffer)) return existingBuffer;

            byte[] data;
            ALFormat format;
            int sampleRate;

            if (AssetManager.IsZipMode)
            {
                using var stream = OpenAssetStream(filePath);
                data = WavLoader.LoadWav(stream, out format, out sampleRate); // Требуется перегрузка LoadWav(Stream)
            }
            else
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Аудиофайл не найден: {filePath}");
                data = WavLoader.LoadWav(filePath, out format, out sampleRate);
            }

            int bufferId = AL.GenBuffer();
            AL.BufferData(bufferId, format, data, sampleRate);

            _audioBuffers[key] = bufferId;
            return bufferId;
        }

        public ReanimDefinition LoadAnimation(string animName, string filePath)
        {
            if (_animations.TryGetValue(animName, out var existingAnim)) return existingAnim;

            ReanimDefinition animDef;

            if (AssetManager.IsZipMode)
            {
                using var stream = OpenAssetStream(filePath);
                if (filePath.EndsWith(".compiled", StringComparison.OrdinalIgnoreCase))
                {
                    animDef = ReanimCompiledParser.ParseCompiled(stream); // Требуется поддержка Stream в вашем парсере
                }
                else
                {
                    animDef = ReanimParser.ParseXml(stream);
                }
            }
            else
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Файл анимации не найден: {filePath}");

                if (filePath.EndsWith(".compiled", StringComparison.OrdinalIgnoreCase))
                {
                    animDef = ReanimCompiledParser.ParseCompiled(filePath);
                }
                else
                {
                    animDef = ReanimParser.ParseXml(filePath);
                }
            }

            _animations[animName] = animDef;
            return animDef;
        }

        public void DiscoverAndLoadTextures(string relativeFolder, bool keepLocalPixels = false)
        {
            IEnumerable<string> files;
            if (AssetManager.IsZipMode)
            {
                files = AssetManager.EnumerateZipFiles(relativeFolder, [".png", ".jpg", ".jpeg"]);
            }
            else
            {
                string rootFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath);
                string fullFolderPath = Path.Combine(rootFullPath, relativeFolder);
                if (!Directory.Exists(fullFolderPath)) return;

                files = Directory.EnumerateFiles(fullFolderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
            }

            foreach (string filePath in files)
            {
                // Для Zip режима filePath возвращается уже относительно корня RootPath
                string relativeFilePath = AssetManager.IsZipMode ? filePath : Path.GetRelativePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath), filePath);
                string assetId = AssetManager.GenerateResourceIdFromPath(relativeFilePath);

                try
                {
                    LoadTexture(assetId, filePath, keepLocalPixels);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AssetManager] Ошибка загрузки текстуры {relativeFilePath}: {ex.Message}");
                }
            }
        }

        public delegate void AnimationDiscoveredHandler(string assetId, string relativeFilePath);
        public static event AnimationDiscoveredHandler OnAnimationDiscovered;

        public void DiscoverAndLoadAnimations(string relativeFolder)
        {
            IEnumerable<string> files;
            if (AssetManager.IsZipMode)
            {
                files = AssetManager.EnumerateZipFiles(relativeFolder, [".reanim", ".compiled"]);
            }
            else
            {
                string rootFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath);
                string fullFolderPath = Path.Combine(rootFullPath, relativeFolder);
                if (!Directory.Exists(fullFolderPath)) return;

                files = Directory.GetFiles(fullFolderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".reanim", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".compiled", StringComparison.OrdinalIgnoreCase));
            }

            foreach (string file in files)
            {
                string relative = AssetManager.IsZipMode ? file : Path.GetRelativePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath), file);

                string assetId = AssetManager.GenerateResourceIdFromPath(relative)
                    .Replace("IMAGE_", "")
                    .Replace(".REANIM", "")
                    .Replace(" ", "");

                try
                {
                    LoadAnimation(assetId, file);
                    _registeredAnimations.Add(assetId);
                    OnAnimationDiscovered?.Invoke(assetId, relative);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AssetManager] Ошибка анимации {assetId}: {ex.Message}");
                }
            }
        }

        public void DiscoverAndLoadParticles(string relativeFolder)
        {
            IEnumerable files;
            if (AssetManager.IsZipMode)
            {
                files = AssetManager.EnumerateZipFiles(relativeFolder, [".xml"]);
            }
            else
            {
                string rootFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath);
                string fullFolderPath = Path.Combine(rootFullPath, relativeFolder);
                if (!Directory.Exists(fullFolderPath)) return;
                files = Directory.GetFiles(fullFolderPath, "*.xml", SearchOption.AllDirectories);
            }
            foreach (string file in files)
            {
                string relative = AssetManager.IsZipMode ? file : Path.GetRelativePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath), file);
                string assetId = AssetManager.GenerateResourceIdFromPath(relative).Replace("IMAGE_", "PARTICLE_");
                try
                {
                    LoadParticleSystem(assetId, file);
                    _registeredParticles.Add(assetId);
                }
                catch (Exception ex) { Console.WriteLine($"[AssetManager] Ошибка частиц {assetId}: {ex.Message}"); }
            }
        }
        public void DiscoverAndLoadAudio(string relativeFolder)
        {
            IEnumerable files;
            if (AssetManager.IsZipMode)
            {
                files = AssetManager.EnumerateZipFiles(relativeFolder, [".wav"]);
            }
            else
            {
                string rootFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath);
                string fullFolderPath = Path.Combine(rootFullPath, relativeFolder);
                if (!Directory.Exists(fullFolderPath)) return;
                files = Directory.GetFiles(fullFolderPath, "*.wav", SearchOption.AllDirectories);
            }
            foreach (string file in files)
            {
                string relative = AssetManager.IsZipMode ? file : Path.GetRelativePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssetManager.RootPath), file);
                string prefix = relative.Contains("music", StringComparison.OrdinalIgnoreCase) ? "MUSIC_" : "SOUND_";
                string assetId = prefix + Path.GetFileNameWithoutExtension(relative).ToUpper();
                try
                {
                    int bufferId = LoadAudio(assetId, file);
                    _audioBuffers[assetId] = bufferId;
                }
                catch (Exception ex) { Console.WriteLine($"[AssetManager] Ошибка аудио {assetId}: {ex.Message}"); }
            }
        }
        public ParticleSystemDefinition LoadParticleSystem(string sysName, string filePath)
        {
            var sysDef = new ParticleSystemDefinition();
            var root = new XmlDocument();
            string rawXml;
            if (AssetManager.IsZipMode)
            {
                using var stream = OpenAssetStream(filePath);
                using var reader = new StreamReader(stream);
                rawXml = reader.ReadToEnd();
            }
            else
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Файл системы частиц не найден: {filePath}");
                rawXml = File.ReadAllText(filePath);
            }
            string validXml = $"<particle_file>{rawXml}</particle_file>";
            root.LoadXml(validXml);
            XmlNode? doc = root.SelectSingleNode("particle_file");
            if (doc == null) return sysDef;
            XmlNodeList? emitterNodes = doc.SelectNodes("Emitter");
            foreach (XmlNode node in emitterNodes)
            {
                var eDef = new EmitterDefinition
                {
                    Name = node.SelectSingleNode("Name")?.InnerText?.Trim() ?? "Unknown",
                    SystemDuration = FloatTrack.Parse(node.SelectSingleNode("SystemDuration")?.InnerText),
                    SpawnMinActive = FloatTrack.Parse(node.SelectSingleNode("SpawnMinActive")?.InnerText),
                    SpawnMaxLaunched = FloatTrack.Parse(node.SelectSingleNode("SpawnMaxLaunched")?.InnerText),
                    EmitterBoxX = FloatTrack.Parse(node.SelectSingleNode("EmitterBoxX")?.InnerText),
                    EmitterBoxY = FloatTrack.Parse(node.SelectSingleNode("EmitterBoxY")?.InnerText),
                    ParticleDuration = FloatTrack.Parse(node.SelectSingleNode("ParticleDuration")?.InnerText),
                    LaunchSpeed = FloatTrack.Parse(node.SelectSingleNode("LaunchSpeed")?.InnerText),
                    ParticleRed = FloatTrack.Parse(node.SelectSingleNode("ParticleRed")?.InnerText),
                    ParticleGreen = FloatTrack.Parse(node.SelectSingleNode("ParticleGreen")?.InnerText),
                    ParticleBlue = FloatTrack.Parse(node.SelectSingleNode("ParticleBlue")?.InnerText),
                    ParticleAlpha = FloatTrack.Parse(node.SelectSingleNode("ParticleAlpha")?.InnerText),
                    ParticleBrightness = FloatTrack.Parse(node.SelectSingleNode("ParticleBrightness")?.InnerText),
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
                var emitterType = node.SelectSingleNode("EmitterType");
                if (emitterType != null)
                {
                    eDef.Type = emitterType.InnerText switch
                    {
                        "Box" => EmitterType.Box,
                        "Circle" => EmitterType.Circle,
                        "BoxPath" => EmitterType.BoxPath,
                        "CirclePath" => EmitterType.CirclePath,
                        "CircleEvenSpacing" => EmitterType.CircleEvenSpacing,
                        _ => eDef.Type
                    };
                }
                string? randSpinText = node.SelectSingleNode("RandomLaunchSpin")?.InnerText?.Trim();
                eDef.RandomLaunchSpin = randSpinText == "1" || randSpinText == "true";
                string? additiveText = node.SelectSingleNode("Additive")?.InnerText?.Trim();
                eDef.Additive = additiveText == "1" || additiveText == "true";
                string? fullScreenText = node.SelectSingleNode("FullScreen")?.InnerText?.Trim();
                eDef.FullScreen = fullScreenText == "1" || fullScreenText == "true";
                if (int.TryParse(node.SelectSingleNode("ImageRow")?.InnerText?.Trim(), out int row)) eDef.ImageRow = row;
                if (int.TryParse(node.SelectSingleNode("ImageCol")?.InnerText?.Trim(), out int col)) eDef.ImageCol = col;
                if (int.TryParse(node.SelectSingleNode("ImageFrames")?.InnerText?.Trim(), out int frames)) eDef.ImageFrames = frames;
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
                eDef.ParticleStretch.SetDefault(1.0f);
                eDef.CollisionReflect.SetDefault(0.0f);
                eDef.CollisionSpin.SetDefault(0.0f);
                eDef.AnimationRate.SetDefault(0.0f);
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
                XmlNodeList? fieldNodes = node.SelectNodes("Field");
                if (fieldNodes != null)
                {
                    foreach (XmlNode fNode in fieldNodes)
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
                XmlNodeList? sysFieldNodes = node.SelectNodes("SystemField");
                if (sysFieldNodes != null)
                {
                    foreach (XmlNode sfNode in sysFieldNodes)
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
        public int GetAudio(string audioId)
        {
            if (_audioBuffers.TryGetValue(audioId.ToUpper(), out int id)) return id;
            return 0;
        }
        public ReanimDefinition GetAnimation(string animName)
        {
            if (_animations.TryGetValue(animName, out var anim)) return anim;
            throw new KeyNotFoundException($"Анимация '{animName}' не найдена в группе '{Name}'.");
        }
        public ParticleSystemDefinition GetParticle(string particleName)
        {
            if (_particles.TryGetValue(particleName, out var particle)) return particle;
            throw new KeyNotFoundException($"Частицы '{particleName}' не найдены в группе '{Name}'.");
        }
        public FontRenderer GetFont(string fontName, int fontSize)
        {
            string key = $"{fontName}{fontSize}";
            if (_fonts.TryGetValue(key, out var font)) return font;
            throw new KeyNotFoundException($"Шрифт '{fontName}' {fontSize}px не найден в группе '{Name}'.");
        }
        public void Dispose()
        {
            _fonts.Clear();
            _animations.Clear();
            _particles.Clear();
            _registeredTextures.Clear();
            foreach (var bufferId in _audioBuffers.Values)
            {
                AL.DeleteBuffer(bufferId);
            }
            _audioBuffers.Clear();
            if (Atlas != null)
            {
                Atlas.Dispose();
                Console.WriteLine($"[AssetManager] Группа '{Name}' полностью выгружена.");
            }
        }
    }
    public static class AssetManager
    {
        private static readonly Dictionary<string, AssetGroup> _groups = new();
        public static string RootPath { get; set; } = "";
        public static AssetGroup Active;
        // Новые поля для ZipArchive
        private static ZipArchive? _zipArchive;
        private static List<string> _zipFilePaths = [];
        public static bool IsZipMode => _zipArchive != null;
        private static readonly DefLoadResPath[] _defLoadResPaths =
        [
            new("IMAGE_REANIM_", "reanim\\"),
            new("REANIM_", "animations\\"),
            new("IMAGE_", "particles\\"),
            new("SOUND_", "sounds\\"),
            new("MUSIC_", "music\\"),
            new("IMAGE_REANIM_", "images\\"),
            new("IMAGE_", "")
        ];
        /// 
        /// Инициализирует менеджер для работы с архивом. Если передать null, вернется к работе с диском.
        /// 
        public static void InitializeZipArchive(string? zipPath)
        {
            if (_zipArchive != null)
            {
                _zipArchive.Dispose();
                _zipArchive = null;
                _zipFilePaths.Clear();
            }
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath)) return;
            // Открываем архив один раз в режиме Read.
            // Обратите внимание: объект _zipArchive должен жить всё время работы игры.
            _zipArchive = ZipFile.OpenRead(zipPath);
            // Кэшируем нормализованные пути всех файлов внутри архива для быстрого поиска
            _zipFilePaths = _zipArchive.Entries
            .Select(e => e.FullName.Replace('/', '\\'))
            .ToList();
            Console.WriteLine($"[AssetManager] Успешно загружен игровой архив '{zipPath}'. Индексировано файлов: {_zipFilePaths.Count}");
        }
        // Поиск файлов в кэше ZIP-архива по виртуальной папке и расширениям
        public static IEnumerable<string> EnumerateZipFiles(string relativeFolder, string[] extensions)
        {
            // Формируем искомый путь внутри ZIP, учитывая ваш RootPath (если ассеты лежат в подпапке архива)
            string targetFolder = Path.Combine(RootPath, relativeFolder).Replace('/', '\\').Trim('\\').ToLower();
        if (!string.IsNullOrEmpty(targetFolder)) targetFolder += "\\";
        foreach (var path in _zipFilePaths)
            {
                string lowerPath = path.ToLower();
                if (lowerPath.StartsWith(targetFolder))
                {
                    if (extensions.Any(ext => lowerPath.EndsWith(ext)))
                    {
                        // Возвращаем исходный путь внутри ZIP
                        yield return path;
                    }
                }
            }
        }
        public static Stream OpenZipStream(string zipEntryPath)
        {
            // Заменяем слеши под формат ZipArchive
            string entryKey = zipEntryPath.Replace('\\', '/');
            var entry = _zipArchive?.GetEntry(entryKey);
            if (entry == null)
            {
                // Попробуем найти без учета регистра на случай несовпадений
                entry = _zipArchive?.Entries.FirstOrDefault(e => e.FullName.Equals(entryKey, StringComparison.OrdinalIgnoreCase));
            }
            if (entry == null) throw new FileNotFoundException($"Ресурс не найден в архиве: {zipEntryPath}");

            // ВАЖНОЕ ИСПРАВЛЕНИЕ: Читаем данные из Zip, но заворачиваем их в MemoryStream,
            // чтобы сторонние библиотеки (StbImageSharp, XmlDocument и т.д.) могли свободно делать Seek.
            using var zipStream = entry.Open();
            var memoryStream = new MemoryStream();
            zipStream.CopyTo(memoryStream);

            // Сбрасываем позицию в ноль, чтобы читающие методы начали чтение с самого начала файла
            memoryStream.Position = 0;

            return memoryStream;
        }

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

            // 1. Сразу приводим все слеши к системному виду (\)
            string pathForId = relativeFilePath.Replace('/', '\\');

            // 2. Отсекаем RootPath из начала пути, если он задан
            if (!string.IsNullOrEmpty(RootPath))
            {
                string normalizedRoot = RootPath.Replace('/', '\\');
                if (pathForId.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    pathForId = pathForId[normalizedRoot.Length..].TrimStart('\\');
                }
            }

            // 3. Переводим путь в нижний регистр для корректного поиска папки
            string normalizedPath = pathForId.ToLower();
            string directoryName = Path.GetDirectoryName(normalizedPath)?.Trim('\\') ?? "";
            string fileName = Path.GetFileName(normalizedPath) ?? "";

            // ВАЖНОЕ ИСПРАВЛЕНИЕ: Полностью вычищаем ВСЕ расширения файла.
            // Если файл в архиве был "blover.reanim" или "zombie.png", останется просто "blover" или "zombie".
            string fileNameWithoutExt = fileName;
            while (Path.HasExtension(fileNameWithoutExt))
            {
                fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileNameWithoutExt);
            }

            // 4. Ищем подходящий префикс PopCap на основе имени папки
            string prefix = "IMAGE_"; // Дефолтный префикс

            foreach (var resPath in _defLoadResPaths)
            {
                string configDir = resPath.Directory.Replace('/', '\\').Trim('\\').ToLower();

                if (!string.IsNullOrEmpty(configDir) &&
                    (directoryName == configDir || directoryName.StartsWith(configDir + "\\")))
                {
                    prefix = resPath.Prefix;
                    break;
                }
            }

            // 5. Очищаем имя файла от пробелов и тире
            string cleanFileName = fileNameWithoutExt.Replace('-', '_').Replace(" ", "");

            // 6. Собираем финальный ID, переводим в UPPERCASE и на всякий случай 
            // страхуемся от потери символа '_' на стыке префикса
            string finalId = (prefix + cleanFileName).ToUpper();

            // Фикс для анимаций: если в префиксе уже была зашита точка или расширение, 
            // или если после замен вылезли дубликаты вроде ".REANIM"
            finalId = finalId.Replace(".REANIM", "");

            return finalId;
        }

        public static TextureRegion? GetTexture(string assetName)
        {
            if (assetName == null) return null;
            string cleanAssetName = assetName.Replace(" ", "").ToUpper();
            if (!cleanAssetName.StartsWith("IMAGE_") && Active != null) cleanAssetName = "IMAGE_REANIM_" + cleanAssetName;
            return Active?.Atlas.GetRegion($"{Active.Name}/{cleanAssetName}");
        }
        public static ParticleSystemDefinition? GetParticle(string particleName) => Active?.GetParticle(particleName);
        public static ReanimDefinition? GetAnimation(string animName) => Active?.GetAnimation(animName);
        public static FontRenderer? GetFont(string fontName, int fontSize) => Active?.GetFont(fontName, fontSize);
        public static int GetAudio(string assetName) => Active != null ? Active.GetAudio(assetName) : 0;
        public static void UnloadAll()
        {
            foreach (var group in _groups.Values) group.Dispose();
            _groups.Clear();
            if (_zipArchive != null)
            {
                _zipArchive.Dispose();
                _zipArchive = null;
                _zipFilePaths.Clear();
            }
            GC.Collect();
        }
    }
}