using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace KrutolFramework.Core
{
    public static class ReanimCompiledParser
    {
        private const int MagicZlib = -559022380; // 0xDEADBEEF (в int32)

        // Старый метод для обратной совместимости (чтение с диска)
        public static ReanimDefinition ParseCompiled(string filePath)
        {
            using (FileStream fs = File.OpenRead(filePath))
            {
                return ParseCompiled(fs);
            }
        }

        // НОВЫЙ МЕТОД: Чтение скомпилированной анимации прямо из потока архива
        public static ReanimDefinition ParseCompiled(Stream fs)
        {
            // Проверяем сжатие Zlib
            byte[] magicBytes = new byte[4];
            fs.Read(magicBytes, 0, 4);
            int magic = BitConverter.ToInt32(magicBytes, 0);

            // Сбрасываем позицию назад, так как прочитали 4 байта
            if (fs.CanSeek)
            {
                fs.Position -= 4;
            }
            else
            {
                // Если поток из Zip-архива не поддерживает Seek (или обратный сдвиг),
                // мы конкатенируем эти 4 байта или создаем MemoryStream, чтобы не сломать парсер.
                MemoryStream msFull = new MemoryStream();
                msFull.Write(magicBytes, 0, 4);
                fs.CopyTo(msFull);
                msFull.Position = 0;
                fs = msFull;
            }

            if (magic == MagicZlib)
            {
                // Файл сжат ZLib
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true))
                {
                    br.ReadInt32(); // Пропускаем magic
                    int decompressedSize = br.ReadInt32(); // Размер распакованных данных

                    using (ZLibStream zlib = new ZLibStream(fs, CompressionMode.Decompress, leaveOpen: true))
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
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true))
                {
                    return ReadBinaryData(br);
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

            var tracksTempData = new (int TransformCount, ReanimTrack Track)[tracksCount];
            for (int i = 0; i < tracksCount; i++)
            {
                br.ReadInt32(); // Пропускаем 0
                br.ReadInt32(); // Пропускаем 0
                int transformCount = br.ReadInt32();

                var track = new ReanimTrack();
                track.Transforms = new ReanimTransform[transformCount];

                tracksTempData[i] = (transformCount, track);
            }

            for (int i = 0; i < tracksCount; i++)
            {
                var (transformCount, track) = tracksTempData[i];

                track.Name = ReadStringByInt32Head(br);
                br.ReadInt32(); // Пропускаем размер блока (0x2C)

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

                    br.BaseStream.Position += 12; // Пропускаем 12 пустых байт

                    track.Transforms[j] = ts;
                }

                for (int j = 0; j < transformCount; j++)
                {
                    var ts = track.Transforms[j];
                    ts.ImageName = ReadStringByInt32Head(br);
                    ts.Font = ReadStringByInt32Head(br);
                    ts.Text = ReadStringByInt32Head(br);
                }

                animDef.Tracks.Add(track);
            }

            return animDef;
        }

        private static float? ReadFloatWithNullCheck(BinaryReader br)
        {
            float val = br.ReadSingle();
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
