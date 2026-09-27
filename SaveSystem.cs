using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KrutolFramework.Core
{
    /// <summary>
    /// Тип слота сохранения уровня (аналог PvZ: adventure, survival, minigames и т.д.).
    /// </summary>
    public enum LevelSlotType
    {
        Adventure = 0,
        SurvivalDay = 1,
        SurvivalNight = 2,
        SurvivalPool = 3,
        SurvivalFog = 4,
        SurvivalRoof = 5,
        SurvivalHardcoreDay = 6,
        SurvivalHardcoreNight = 7,
        SurvivalHardcorePool = 8,
        SurvivalHardcoreFog = 9,
        SurvivalHardcoreRoof = 10,
        SurvivalEndlessDay = 11,
        SurvivalEndlessNight = 12,
        SurvivalEndlessPool = 13,
        SurvivalEndlessFog = 14,
        SurvivalEndlessRoof = 15,
        MinigameStart = 16,
        MinigameEnd = 35,
        VasebreakerStart = 51,
        VasebreakerEnd = 60,
        IZombieStart = 61,
        IZombieEnd = 70
    }

    /// <summary>
    /// Профиль игрока (аналог user#.dat в PvZ).
    /// </summary>
    public class UserProfile
    {
        public int UserId { get; set; }

        // Основная прогрессия
        public int AdventureLevel { get; set; }      // 1..50 (1-1 .. 5-10)
        public long Coins { get; set; }

        // Открытые режимы / флаги
        public bool SurvivalUnlocked { get; set; }
        public bool MinigamesUnlocked { get; set; }
        public bool VasebreakerUnlocked { get; set; }
        public bool IZombieUnlocked { get; set; }
        public bool ZenGardenUnlocked { get; set; }

        // Трофеи / достижения (упрощённо)
        public int Trophies { get; set; }
        public List<string> Achievements { get; set; } = new();

        // Покупки / апгрейды
        public List<string> PurchasedItems { get; set; } = new();

        // Zen Garden (упрощённо: список растений по id)
        public List<ZenPlant> ZenGardenPlants { get; set; } = new();

        // Zombatar / кастомизация (можно расширить)
        public string? ZombatarPreset { get; set; }

        // Дополнительно: статистика, время игры и т.п.
        public TimeSpan TotalPlayTime { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
    }

    /// <summary>
    /// Растение в Zen Garden (упрощённая модель).
    /// </summary>
    public class ZenPlant
    {
        public string PlantId { get; set; } = "";
        public int Level { get; set; }
        public DateTime LastWateredUtc { get; set; }
        public bool NeedsWater => DateTime.UtcNow - LastWateredUtc > TimeSpan.FromHours(4);
    }

    /// <summary>
    /// Сохранение конкретного уровня (аналог game#_#.dat в PvZ).
    /// </summary>
    public class LevelSave
    {
        public int UserId { get; set; }
        public int Slot { get; set; }          // 0..70 по логике PvZ
        public LevelSlotType SlotType => GetSlotType(Slot);

        // Состояние уровня: волны, растения, зомби, снаряды и т.д.
        // Здесь — скелет, который ты наполнишь под свою игру.
        public int CurrentWave { get; set; }
        public double TimeSeconds { get; set; }

        public List<LevelPlant> Plants { get; set; } = new();
        public List<LevelZombie> Zombies { get; set; } = new();
        public List<LevelProjectile> Projectiles { get; set; } = new();

        // Флаги
        public bool IsPaused { get; set; }
        public bool HasExitedMidLevel { get; set; }

        // Дополнительно: состояние газонов, солнца, ресурсов и т.д.
        public int Sun { get; set; }
        public int LawnMowersLeft { get; set; }

        private static LevelSlotType GetSlotType(int slot)
        {
            if (slot == 0) return LevelSlotType.Adventure;
            if (slot >= 1 && slot <= 15)
            {
                return slot switch
                {
                    1 => LevelSlotType.SurvivalDay,
                    2 => LevelSlotType.SurvivalNight,
                    3 => LevelSlotType.SurvivalPool,
                    4 => LevelSlotType.SurvivalFog,
                    5 => LevelSlotType.SurvivalRoof,
                    6 => LevelSlotType.SurvivalHardcoreDay,
                    7 => LevelSlotType.SurvivalHardcoreNight,
                    8 => LevelSlotType.SurvivalHardcorePool,
                    9 => LevelSlotType.SurvivalHardcoreFog,
                    10 => LevelSlotType.SurvivalHardcoreRoof,
                    11 => LevelSlotType.SurvivalEndlessDay,
                    12 => LevelSlotType.SurvivalEndlessNight,
                    13 => LevelSlotType.SurvivalEndlessPool,
                    14 => LevelSlotType.SurvivalEndlessFog,
                    15 => LevelSlotType.SurvivalEndlessRoof,
                    _ => LevelSlotType.SurvivalDay
                };
            }

            if (slot >= 16 && slot <= 35) return LevelSlotType.MinigameStart;
            if (slot >= 51 && slot <= 60) return LevelSlotType.VasebreakerStart;
            if (slot >= 61 && slot <= 70) return LevelSlotType.IZombieStart;

            throw new ArgumentOutOfRangeException(nameof(slot), $"Invalid PvZ-style slot: {slot}");
        }
    }

    /// <summary>
    /// Упрощённое представление растения на уровне.
    /// </summary>
    public class LevelPlant
    {
        public string PlantId { get; set; } = "";
        public int GridX { get; set; }
        public int GridY { get; set; }
        public double Health { get; set; }
        public double LastAttackTime { get; set; }
        public int Level { get; set; }
    }

    /// <summary>
    /// Упрощённое представление зомби на уровне.
    /// </summary>
    public class LevelZombie
    {
        public string ZombieId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public double Health { get; set; }
        public double Speed { get; set; }
        public string? State { get; set; } // "walking", "eating", "dying" и т.д.
    }

    /// <summary>
    /// Упрощённое представление снаряда.
    /// </summary>
    public class LevelProjectile
    {
        public string ProjectileId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public double Damage { get; set; }
    }

    /// <summary>
    /// Мета-файл users.dat: список доступных профилей и их базовая инфа.
    /// </summary>
    public class UsersMeta
    {
        public List<UserMetaEntry> Users { get; set; } = new();
    }

    public class UserMetaEntry
    {
        public int UserId { get; set; }
        public string? Name { get; set; }
        public int AdventureLevel { get; set; }
        public long Coins { get; set; }
        public int Trophies { get; set; }
    }

    /// <summary>
    /// Основная система сохранений в стиле PvZ 1.
    /// </summary>
    public static class SaveSystem
    {
        private static readonly string _saveDirectory;
        private static readonly JsonSerializerOptions _jsonOptions;

        static SaveSystem()
        {
            _saveDirectory = "saves";
            Directory.CreateDirectory(_saveDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        #region Helpers

        private static string UsersMetaPath => Path.Combine(_saveDirectory, "users.dat");

        private static string UserProfilePath(int userId) =>
            Path.Combine(_saveDirectory, $"user{userId}.dat");

        private static string LevelSavePath(int userId, int slot) =>
            Path.Combine(_saveDirectory, $"game{userId}_{slot}.dat");

        private static UsersMeta LoadUsersMeta()
        {
            var path = UsersMetaPath;
            if (!File.Exists(path))
                return new UsersMeta();

            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UsersMeta>(text, _jsonOptions) ?? new UsersMeta();
        }

        private static void SaveUsersMeta(UsersMeta meta)
        {
            var text = JsonSerializer.Serialize(meta, _jsonOptions);
            File.WriteAllText(UsersMetaPath, text);
        }

        #endregion

        #region Users / Profiles

        /// <summary>
        /// Загружает список всех профилей (users.dat).
        /// </summary>
        public static IReadOnlyList<UserMetaEntry> LoadUsers()
        {
            var meta = LoadUsersMeta();
            return meta.Users.AsReadOnly();
        }

        /// <summary>
        /// Сохраняет обновлённый список профилей (users.dat).
        /// </summary>
        public static void SaveUsers(IReadOnlyList<UserMetaEntry> users)
        {
            var meta = new UsersMeta { Users = new List<UserMetaEntry>(users) };
            SaveUsersMeta(meta);
        }

        /// <summary>
        /// Загружает профиль игрока (user#.dat).
        /// </summary>
        public static UserProfile? LoadProfile(int userId)
        {
            var path = UserProfilePath(userId);
            if (!File.Exists(path))
                return null;

            var text = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<UserProfile>(text, _jsonOptions);
            if (profile != null)
                profile.UserId = userId;
            return profile;
        }

        /// <summary>
        /// Удаляет пользователя и все его сохранения (user#.dat, game#_*.dat, запись в users.dat).
        /// </summary>
        public static void DeleteUser(int userId)
        {
            // 1. Удаляем профиль user#.dat
            var profilePath = UserProfilePath(userId);
            if (File.Exists(profilePath))
                File.Delete(profilePath);

            // 2. Удаляем все mid-level сохранения game#_*.dat
            for (int slot = 0; slot <= 70; slot++)
            {
                var levelPath = LevelSavePath(userId, slot);
                if (File.Exists(levelPath))
                    File.Delete(levelPath);
            }

            // 3. Обновляем users.dat
            var meta = LoadUsersMeta();
            var entry = meta.Users.Find(u => u.UserId == userId);
            if (entry != null)
            {
                meta.Users.Remove(entry);
                SaveUsersMeta(meta);
            }
        }

        /// <summary>
        /// Сохраняет профиль игрока (user#.dat) и обновляет users.dat.
        /// </summary>
        public static void SaveProfile(UserProfile profile)
        {
            var path = UserProfilePath(profile.UserId);
            var text = JsonSerializer.Serialize(profile, _jsonOptions);
            File.WriteAllText(path, text);

            // Обновляем users.dat
            var meta = LoadUsersMeta();
            var entry = meta.Users.Find(u => u.UserId == profile.UserId);
            if (entry == null)
            {
                entry = new UserMetaEntry { UserId = profile.UserId };
                meta.Users.Add(entry);
            }

            entry.Name ??= $"Player {profile.UserId}";
            entry.AdventureLevel = profile.AdventureLevel;
            entry.Coins = profile.Coins;
            entry.Trophies = profile.Trophies;

            SaveUsersMeta(meta);
        }

        /// <summary>
        /// Создаёт новый профиль с новым UserId.
        /// </summary>
        public static UserProfile CreateNewProfile(string? name = null)
        {
            var meta = LoadUsersMeta();
            int nextId = 1;
            if (meta.Users.Count > 0)
                nextId = meta.Users.Max(u => u.UserId) + 1;

            var profile = new UserProfile
            {
                UserId = nextId,
                AdventureLevel = 1,
                Coins = 0,
                Trophies = 0,
                ZenGardenUnlocked = false,
                SurvivalUnlocked = false,
                MinigamesUnlocked = false,
                VasebreakerUnlocked = false,
                IZombieUnlocked = false
            };

            SaveProfile(profile);
            return profile;
        }

        #endregion

        #region Level Saves (game#_#.dat)

        /// <summary>
        /// Возвращает список доступных слотов сохранений для пользователя.
        /// </summary>
        public static IReadOnlyList<int> GetAvailableLevelSlots(int userId)
        {
            var result = new List<int>();
            // PvZ-логика: 0..70
            for (int slot = 0; slot <= 70; slot++)
            {
                var path = LevelSavePath(userId, slot);
                if (File.Exists(path))
                    result.Add(slot);
            }
            return result.AsReadOnly();
        }

        /// <summary>
        /// Загружает сохранение уровня (game#_#.dat).
        /// </summary>
        public static LevelSave? LoadLevelSave(int userId, int slot)
        {
            var path = LevelSavePath(userId, slot);
            if (!File.Exists(path))
                return null;

            var text = File.ReadAllText(path);
            var save = JsonSerializer.Deserialize<LevelSave>(text, _jsonOptions);
            if (save != null)
            {
                save.UserId = userId;
                save.Slot = slot;
            }
            return save;
        }

        /// <summary>
        /// Сохраняет уровень (game#_#.dat).
        /// </summary>
        public static void SaveLevelSave(LevelSave save)
        {
            var path = LevelSavePath(save.UserId, save.Slot);
            var text = JsonSerializer.Serialize(save, _jsonOptions);
            File.WriteAllText(path, text);
        }

        /// <summary>
        /// Удаляет сохранение уровня.
        /// </summary>
        public static void DeleteLevelSave(int userId, int slot)
        {
            var path = LevelSavePath(userId, slot);
            if (File.Exists(path))
                File.Delete(path);
        }

        #endregion

        #region Convenience

        /// <summary>
        /// Проверяет, есть ли активное mid-level сохранение для пользователя и слота.
        /// </summary>
        public static bool HasMidLevelSave(int userId, int slot)
        {
            var path = LevelSavePath(userId, slot);
            return File.Exists(path);
        }

        /// <summary>
        /// Очищает все mid-level сохранения для пользователя.
        /// </summary>
        public static void ClearAllMidLevelSaves(int userId)
        {
            for (int slot = 0; slot <= 70; slot++)
            {
                var path = LevelSavePath(userId, slot);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        #endregion
    }
}