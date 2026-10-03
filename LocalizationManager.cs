using System;
using System.Collections.Generic;
using System.IO;

namespace KrutolFramework.Core
{
    public static class LocalizationManager
    {
        private static readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
        public static string CurrentLanguage { get; private set; } = "en";

        /// <summary>
        /// Загружает оригинальный файл строк PvZ (например, из папки properties/LawnStrings_ru.txt)
        /// </summary>
        public static void LoadLanguage(string langCode, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[Loc Error] Файл локализации не найден: {filePath}");
                return;
            }

            _strings.Clear();
            CurrentLanguage = langCode;

            string[] lines = File.ReadAllLines(filePath);
            string currentKey = "";
            string currentText = "";

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("//") || string.IsNullOrEmpty(line)) continue;

                // Поиск тегов [IDENTIFIER]
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (!string.IsNullOrEmpty(currentKey))
                    {
                        _strings[currentKey] = currentText.Trim().Replace("\\n", "\n");
                    }
                    currentKey = line.Substring(1, line.Length - 2);
                    currentText = "";
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentKey))
                    {
                        currentText += (string.IsNullOrEmpty(currentText) ? "" : "\n") + rawLine;
                    }
                }
            }

            // Записываем последнюю строку
            if (!string.IsNullOrEmpty(currentKey))
            {
                _strings[currentKey] = currentText.Trim().Replace("\\n", "\n");
            }

            Console.WriteLine($"[Localization] Успешно загружен язык '{langCode}'. Всего строк: {_strings.Count}");
        }

        /// <summary>
        /// Возвращает переведенную строку по её ID (аналог TodStringTranslate)
        /// </summary>
        public static string Translate(string key, string defaultValue = "")
        {
            if (_strings.TryGetValue(key, out string text)) return text;
            return string.IsNullOrEmpty(defaultValue) ? $"[{key}]" : defaultValue;
        }

        public static string _T(string key) => Translate(key);
    }
}
