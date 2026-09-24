
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace KrutolFramework.Core
{
    public class ModManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string AssemblyName { get; set; }
    }

    public static class ModManager
    {
        private static readonly Dictionary<string, string> ModDirectories = new();
        public static List<ModManifest> LoadedMods { get; } = new();

        public static void InitAndLoadMods(string modsRootPath)
        {
            if (!Directory.Exists(modsRootPath))
                Directory.CreateDirectory(modsRootPath);

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            string[] modFolders = Directory.GetDirectories(modsRootPath);

            // Настройки десериализации (нечувствительность к регистру букв JSON)
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (string modFolder in modFolders)
            {
                string manifestPath = Path.Combine(modFolder, "mod.manifest");
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    string json = File.ReadAllText(manifestPath);
                    ModManifest manifest = JsonSerializer.Deserialize<ModManifest>(json, jsonOptions);

                    if (manifest == null) continue;

                    string mainDllPath = Path.Combine(modFolder, manifest.AssemblyName);
                    if (!File.Exists(mainDllPath))
                    {
                        Console.WriteLine($"[Error] Исполняемый файл {manifest.AssemblyName} не найден для мода {manifest.Name}");
                        continue;
                    }

                    string assemblyNameWithoutExt = Path.GetFileNameWithoutExtension(manifest.AssemblyName);
                    ModDirectories[assemblyNameWithoutExt] = modFolder;

                    // Загружаем сборку в контекст приложения
                    Assembly asm = Assembly.LoadFrom(mainDllPath);

                    bool initFound = false;
                    foreach (Type type in asm.GetTypes())
                    {
                        // Ищем метод public static void Init()
                        MethodInfo initMethod = type.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
                        if (initMethod != null)
                        {
                            initMethod.Invoke(null, null);
                            initFound = true;
                            Console.WriteLine($"[Mods] Мод '{manifest.Name}' v{manifest.Version} успешно загружен.");
                            LoadedMods.Add(manifest);
                            break;
                        }
                    }

                    if (!initFound)
                    {
                        Console.WriteLine($"[Warning] Мод '{manifest.Name}' загружен, но static void Init() не найден.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Ошибка при загрузке мода из {modFolder}: {ex.Message}");
                }
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requestingName = new AssemblyName(args.Name);
            string shortName = requestingName.Name;

            Assembly requestingAssembly = args.RequestingAssembly;
            string modFolder = null;

            if (requestingAssembly != null)
            {
                ModDirectories.TryGetValue(requestingAssembly.GetName().Name, out modFolder);
            }

            if (!string.IsNullOrEmpty(modFolder))
            {
                string dependencyPath = Path.Combine(modFolder, shortName + ".dll");
                if (File.Exists(dependencyPath)) return Assembly.LoadFrom(dependencyPath);
            }

            foreach (string folder in ModDirectories.Values)
            {
                string dependencyPath = Path.Combine(folder, shortName + ".dll");
                if (File.Exists(dependencyPath)) return Assembly.LoadFrom(dependencyPath);
            }

            return null;
        }
    }
}

