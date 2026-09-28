using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace KrutolFramework.Core
{
    public static class ReanimCompiledParser
    {
        private const int MagicZlib = -559022380; // 0xDEADBEEF (в int32)

        public static ReanimDefinition ParseCompiled(string filePath)
        {
            using (FileStream fs = File.OpenRead(filePath))
            {
                // Проверяем сжатие Zlib
                byte[] magicBytes = new byte[4];
                fs.Read(magicBytes, 0, 4);
                int magic = BitConverter.ToInt32(magicBytes, 0);

                fs.Position = 0;

                if (magic == MagicZlib)
                {
                    // Файл сжат ZLib
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        br.ReadInt32(); // Пропускаем magic
                        int decompressedSize = br.ReadInt32(); // Размер распакованных данных

                        using (ZLibStream zlib = new ZLibStream(fs, CompressionMode.Decompress))
                        using (MemoryStream ms = new MemoryStream(decompressedSize))
                        {
                            zlib.CopyTo(ms);
                            ms.Position = 0;
                            using (BinaryReader decompressedReader = new BinaryReader(ms, Encoding.UTF8))
                            {
                                return ReadBinaryData(decompressedReader);
                            }
                        }
                    }
                }
                else
                {
                    // Файл не сжат
                    using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
                    {
                        return ReadBinaryData(br);
                    }
                }
            }
        }

        private static ReanimDefinition ReadBinaryData(BinaryReader br)
        {
            var animDef = new ReanimDefinition();

            // Чтение заголовка
            br.ReadInt32(); // Пропускаем заголовок (обычно -1282165568 / 0xB3960000)
            br.ReadInt32(); // Пропускаем 0

            int tracksCount = br.ReadInt32();
            animDef.FPS = br.ReadSingle(); // reanim.fps

            br.ReadInt32(); // Пропускаем 0
            br.ReadInt32(); // Пропускаем маркер структуры (0xC)

            // 1. Сначала читаем заголовки треков (количество трансформаций/кадров в каждом)
            var tracksTempData = new (int TransformCount, ReanimTrack Track)[tracksCount];
            for (int i = 0; i < tracksCount; i++)
            {
                br.ReadInt32(); // Пропускаем 0
                br.ReadInt32(); // Пропускаем 0
                int transformCount = br.ReadInt32();

                var track = new ReanimTrack();
                // Инициализируем массив трансформаций в вашей структуре трека
                track.Transforms = new ReanimTransform[transformCount];

                tracksTempData[i] = (transformCount, track);
            }

            // 2. Читаем данные каждого трека последовательно
            for (int i = 0; i < tracksCount; i++)
            {
                var (transformCount, track) = tracksTempData[i];

                // Читаем имя трека
                track.Name = ReadStringByInt32Head(br);
                br.ReadInt32(); // Пропускаем размер блока (0x2C)

                // Читаем числовые матрицы трансформаций (X, Y, KX, KY, SX, SY, F, A)
                for (int j = 0; j < transformCount; j++)
                {
                    var ts = new ReanimTransform
                    {
                        TransX = ReadFloatWithNullCheck(br),
                        TransY = ReadFloatWithNullCheck(br),
                        SkewX = ReadFloatWithNullCheck(br),
                        SkewY = ReadFloatWithNullCheck(br),
                        ScaleX = ReadFloatWithNullCheck(br),
                        ScaleY = ReadFloatWithNullCheck(br),
                        Frame = ReadFloatWithNullCheck(br),
                        Alpha = ReadFloatWithNullCheck(br)
                    };

                    br.BaseStream.Position += 12; // Пропускаем 12 пустых байт (3 * int32)

                    track.Transforms[j] = ts;
                }

                // Читаем строковые ресурсы трансформаций (Имя картинки, Шрифт, Текст)
                for (int j = 0; j < transformCount; j++)
                {
                    var ts = track.Transforms[j];

                    ts.ImageName = ReadStringByInt32Head(br); // Переменная 'i' в оригинале PopCap
                    ts.Font = ReadStringByInt32Head(br);
                    ts.Text = ReadStringByInt32Head(br);
                }

                // Добавляем готовый трек в определение анимации
                animDef.Tracks.Add(track);
            }

            // Опционально: Рассчитываем общее количество кадров в анимации для FrameCount
            // На основе максимального количества трансформаций среди всех треков
            int maxFrames = 0;
            foreach (var track in animDef.Tracks)
            {
                if (track.Transforms.Length > maxFrames)
                    maxFrames = track.Transforms.Length;
            }

            return animDef;
        }

        private static float? ReadFloatWithNullCheck(BinaryReader br)
        {
            float val = br.ReadSingle();
            // Значение -10000.0f в PopCap используется как индикатор отсутствия данных (null)
            return val == -10000f ? null : val;
        }

        private static string ReadStringByInt32Head(BinaryReader br)
        {
            int length = br.ReadInt32();
            if (length <= 0) return string.Empty;

            byte[] stringBytes = br.ReadBytes(length);
            return Encoding.UTF8.GetString(stringBytes);
        }
    }
}
