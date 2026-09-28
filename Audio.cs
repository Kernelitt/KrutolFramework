using System;
using System.IO;
using OpenTK.Audio.OpenAL;
using System.Collections.Generic;

namespace KrutolFramework.Core
{
    public static class WavLoader
    {
        public static byte[] LoadWav(string filePath, out ALFormat format, out int sampleRate)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Аудиофайл не найден: {filePath}");

            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            // RIFF chunk
            if (new string(reader.ReadChars(4)) != "RIFF")
                throw new NotSupportedException("Неверный формат файла (ожидался RIFF).");

            reader.ReadInt32(); // Размер файла минус 8 байт

            if (new string(reader.ReadChars(4)) != "WAVE")
                throw new NotSupportedException("Неверный формат медиа (ожидался WAVE).");

            // fmt chunk
            if (new string(reader.ReadChars(4)) != "fmt ")
                throw new NotSupportedException("Неверный субчанк (ожидался fmt ).");

            int fmtChunkSize = reader.ReadInt32();
            int audioFormat = reader.ReadInt16(); // 1 для PCM
            int channels = reader.ReadInt16();
            sampleRate = reader.ReadInt32();
            reader.ReadInt32(); // byteRate
            reader.ReadInt16(); // blockAlign
            int bitsPerSample = reader.ReadInt16();

            // Пропускаем оставшиеся байты fmt, если они есть
            if (fmtChunkSize > 16)
                stream.Position += (fmtChunkSize - 16);

            // Ищем chunk "data" (пропуская метаданные LIST и т.д.)
            string dataChunkHeader = new string(reader.ReadChars(4));
            while (dataChunkHeader != "data" && stream.Position < stream.Length)
            {
                int size = reader.ReadInt32();
                stream.Position += size;
                if (stream.Position >= stream.Length) break;
                dataChunkHeader = new string(reader.ReadChars(4));
            }

            if (dataChunkHeader != "data")
                throw new Exception("Не найден аудио-дата чанк в WAV файле.");

            int dataSize = reader.ReadInt32();
            byte[] audioData = reader.ReadBytes(dataSize);

            // Определяем OpenAL формат
            format = channels switch
            {
                1 => bitsPerSample == 8 ? ALFormat.Mono8 : ALFormat.Mono16,
                2 => bitsPerSample == 8 ? ALFormat.Stereo8 : ALFormat.Stereo16,
                _ => throw new NotSupportedException($"Количество каналов ({channels}) не поддерживается.")
            };

            return audioData;
        }
    }

    public static class AudioManager
    {
        private static ALDevice _device;
        private static ALContext _context;

        // Пул источников звука для SFX
        private static readonly List<int> _sfxSources = new();
        private static int _musicSource;

        private static float _masterVolume = 1.0f;
        private static float _sfxVolume = 1.0f;
        private static float _musicVolume = 1.0f;

        public static void Initialize(int maxSfxSources = 32)
        {
            // Инициализация OpenAL контекста
            _device = ALC.OpenDevice(null);
            _context = ALC.CreateContext(_device, (int[])null);
            ALC.MakeContextCurrent(_context);

            // Настройка модели затухания (актуально, если будете делать 3D звук)
            AL.DistanceModel(ALDistanceModel.InverseDistanceClamped);

            // Генерируем пул источников для SFX
            for (int i = 0; i < maxSfxSources; i++)
            {
                int source = AL.GenSource();
                _sfxSources.Add(source);
            }

            // Источник для фоновой музыки
            _musicSource = AL.GenSource();
            AL.Source(_musicSource, ALSourceb.Looping, true); // Музыка обычно циклична
        }

        public static void PlaySfx(int bufferId, float pitch = 1.0f, float volume = 1.0f)
        {
            if (bufferId == 0) return;

            // Ищем свободный (не играющий в данный момент) источник
            int sourceToUse = -1;
            foreach (var source in _sfxSources)
            {
                AL.GetSource(source, ALGetSourcei.SourceState, out int state);
                if ((ALSourceState)state != ALSourceState.Playing)
                {
                    sourceToUse = source;
                    break;
                }
            }

            // Если все заняты — перебиваем первый попавшийся
            if (sourceToUse == -1) sourceToUse = _sfxSources[0];

            // Настраиваем параметры и запускаем
            AL.SourceStop(sourceToUse);
            AL.Source(sourceToUse, ALSourcei.Buffer, bufferId);
            AL.Source(sourceToUse, ALSourcef.Pitch, pitch);
            AL.Source(sourceToUse, ALSourcef.Gain, volume * _sfxVolume * _masterVolume);
            AL.SourcePlay(sourceToUse);
        }

        public static void PlayMusic(int bufferId, bool loop = true)
        {
            if (bufferId == 0) return;

            AL.SourceStop(_musicSource);
            AL.Source(_musicSource, ALSourcei.Buffer, bufferId);
            AL.Source(_musicSource, ALSourceb.Looping, loop);
            AL.Source(_musicSource, ALSourcef.Gain, _musicVolume * _masterVolume);
            AL.SourcePlay(_musicSource);
        }

        public static void StopMusic() => AL.SourceStop(_musicSource);
        public static void PauseMusic() => AL.SourcePause(_musicSource);
        public static void ResumeMusic() => AL.SourcePlay(_musicSource);

        public static void SetVolume(float master, float sfx, float music)
        {
            _masterVolume = Math.Clamp(master, 0f, 1f);
            _sfxVolume = Math.Clamp(sfx, 0f, 1f);
            _musicVolume = Math.Clamp(music, 0f, 1f);

            // Накатываем громкость на играющую музыку мгновенно
            AL.Source(_musicSource, ALSourcef.Gain, _musicVolume * _masterVolume);
        }

        public static void Shutdown()
        {
            StopMusic();
            AL.DeleteSource(_musicSource);

            foreach (var source in _sfxSources)
            {
                AL.DeleteSource(source);
            }
            _sfxSources.Clear();

            // Освобождаем контекст
            if (_context.Handle != IntPtr.Zero)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(_context);
            }
            if (_device.Handle != IntPtr.Zero)
            {
                ALC.CloseDevice(_device);
            }
        }
    }
}
