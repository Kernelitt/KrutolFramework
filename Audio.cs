using ManagedBass;
using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace KrutolFramework.Core
{
    // Вспомогательный класс для хранения декодированных PCM данных
    public class PcmAudioData(byte[] data, ALFormat channels, int sampleRate)
    {
        public byte[] Data = data;
        public ALFormat Channels = channels;
        public int SampleRate = sampleRate;
    }
    public static class OggLoader
    {
        public static byte[] LoadOgg(Stream stream, out ALFormat format, out int sampleRate)
        {
            // ЖЕЛЕЗОБЕТОННЫЙ ФИКС: Сбрасываем позицию потока в 0, 
            // на случай если AssetManager уже читал его или проверял заголовки
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            // Инициализируем VorbisReader
            using var reader = new NVorbis.VorbisReader(stream);

            sampleRate = reader.SampleRate;
            format = reader.Channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16;

            // Используем динамический список для байт, чтобы не зависеть от свойств TotalSamples
            var outputBytes = new List<byte>();

            // Буфер для чтения float-сэмплов блоками (например, по 4096 элементов)
            float[] readBuffer = new float[4096];
            int samplesRead;

            // Читаем файл блоками до тех пор, пока есть данные
            while ((samplesRead = reader.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    // Ограничиваем float в диапазон от -1.0 до 1.0
                    float sample = readBuffer[i];
                    if (sample > 1.0f) sample = 1.0f;
                    else if (sample < -1.0f) sample = -1.0f;

                    // Конвертируем в 16-битное целое число (PCM 16-bit)
                    short shortSample = (short)(sample >= 0.0f ? sample * 32367.0f : sample * 32368.0f);

                    // Раскладываем short на 2 байта (Little Endian)
                    outputBytes.Add((byte)(shortSample & 0xFF));
                    outputBytes.Add((byte)((shortSample >> 8) & 0xFF));
                }
            }

            // Добавим отладочный лог, чтобы увидеть реальный размер массива на выходе
            Console.WriteLine($"[NVorbis Debug] Декодировано из OGG: {outputBytes.Count} байт.");

            return outputBytes.ToArray();
        }
    }
    public static class AudioManager
    {
        private static ALDevice _device;
        private static ALContext _context;

        private static readonly List<int> _sfxSources = [];
        private static readonly Dictionary<string, int> _loadedSfxBuffers = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, int> _loadedMusicTracks = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<int> _activePlayingMusic = [];

        private static float _masterVolume = 1.0f;
        private static float _sfxVolume = 1.0f;
        private static float _musicVolume = 1.0f;

        private static bool _bassInitialized = false;

        public static void Initialize(int maxSfxSources = 32)
        {
            try
            {
                _device = ALC.OpenDevice(null);
                _context = ALC.CreateContext(_device, null as int[]);
                ALC.MakeContextCurrent(_context);

                AL.DistanceModel(ALDistanceModel.None);
                AL.Listener(ALListener3f.Position, 0f, 0f, 0f);
                AL.Listener(ALListenerf.Gain, 1.0f);

                _sfxSources.Clear();
                for (int i = 0; i < maxSfxSources; i++)
                {
                    int source = AL.GenSource();
                    _sfxSources.Add(source);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Ошибка OpenAL: {ex.Message}");
            }

            if (!_bassInitialized)
            {
                if (!Bass.Init(-1, 44100, DeviceInitFlags.NoSpeakerAssignment, IntPtr.Zero))
                {
                    Console.WriteLine($"[AudioManager] Ошибка инициализации BASS: {Bass.LastError}");
                }
                else
                {
                    _bassInitialized = true;
                }
            }

            if (_context.Handle != IntPtr.Zero)
            {
                ALC.MakeContextCurrent(_context);
            }
        }

        #region Звуковые Эффекты (Чистый OpenAL)

        public static void RegisterSfx(string assetName, Stream oggStream)
        {
            if (_context.Handle != IntPtr.Zero) ALC.MakeContextCurrent(_context);

            string key = assetName.ToUpper();
            if (_loadedSfxBuffers.TryGetValue(key, out int oldBufferId))
            {
                AL.DeleteBuffer(oldBufferId);
                _loadedSfxBuffers.Remove(key);
            }

            byte[] pcmData = OggLoader.LoadOgg(oggStream, out ALFormat format, out int sampleRate);

            int bufferId = AL.GenBuffer();
            AL.BufferData(bufferId, format, ref pcmData[0], pcmData.Length, sampleRate);

            _loadedSfxBuffers[key] = bufferId;
            Console.WriteLine($"[OpenAL SFX] Звук {key} успешно загружен в буфер {bufferId}. Размер: {pcmData.Length} байт.");
        }

        public static void RegisterSfx(string assetName, byte[] fileData)
        {
            using var ms = new MemoryStream(fileData);
            RegisterSfx(assetName, ms);
        }

        public static void PlaySfx(string assetName, float pitch = 1.0f, float volume = 1.0f)
        {
            string key = assetName.ToUpper();
            if (_loadedSfxBuffers.TryGetValue(key, out int bufferId))
            {
                PlaySfx(bufferId, pitch, volume);
            }
        }

        public static void PlaySfx(int bufferId, float pitch = 1.0f, float volume = 1.0f)
        {
            if (bufferId == 0) return;

            if (_context.Handle != IntPtr.Zero)
            {
                ALC.MakeContextCurrent(_context);
                ALC.ProcessContext(_context);
            }

            AL.GetError(); 

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

            if (sourceToUse == -1 && _sfxSources.Count > 0) sourceToUse = _sfxSources[0];
            if (sourceToUse == -1) return;

            AL.SourceStop(sourceToUse);
            AL.Source(sourceToUse, ALSourcei.Buffer, 0);

            AL.Source(sourceToUse, ALSourceb.SourceRelative, true);
            AL.Source(sourceToUse, ALSource3f.Position, 0f, 0f, 0f); 
            AL.Source(sourceToUse, ALSourcei.Buffer, bufferId);
            AL.Source(sourceToUse, ALSourcef.Pitch, pitch);

            float finalVolume = volume * _masterVolume * _sfxVolume;
            AL.Source(sourceToUse, ALSourcef.Gain, finalVolume);

            AL.SourcePlay(sourceToUse);

            AL.GetSource(sourceToUse, ALGetSourcei.SourceState, out int checkState);
        }

        public static int GetMusicHandle(string assetName)
        {
            string key = assetName.ToUpper();
            if (_loadedMusicTracks.TryGetValue(key, out int handle))
            {
                return handle;
            }
            return 0;
        }
        public static int GetMusicOrder(string assetName)
        {
            int handle = GetMusicHandle(assetName);

            if (handle != 0)
            {
                return (int)Bass.ChannelGetPosition(handle, PositionFlags.MusicOrders);
            }

            return 0;
        }

        #endregion

        #region Трекерная Музыка (.MO3 -> BASS)

        public static void RegisterMusicTrack(string assetName, byte[] fileData)
        {
            string key = assetName.ToUpper();
            if (_loadedMusicTracks.TryGetValue(key, out int oldHandle))
            {
                _activePlayingMusic.Remove(oldHandle);
                Bass.MusicFree(oldHandle);
            }

            IntPtr pointer = Marshal.AllocHGlobal(fileData.Length);
            Marshal.Copy(fileData, 0, pointer, fileData.Length);

            int musicHandle = Bass.MusicLoad(pointer, 0, fileData.Length, BassFlags.MusicRamp | BassFlags.Loop, 0);
            if (musicHandle == 0)
            {
                Marshal.FreeHGlobal(pointer);
                throw new Exception($"[AudioManager] Ошибка .mo3 '{assetName}': {Bass.LastError}");
            }

            _loadedMusicTracks[key] = musicHandle;
            Bass.ChannelSetAttribute(musicHandle, ChannelAttribute.Volume, 0.15f);
            Bass.ChannelSetAttribute(musicHandle, (ChannelAttribute)2, 15f);
        }

        public static void SetTrackVolume(string assetName, float volume)
        {
            string key = assetName.ToUpper();
            if (_loadedMusicTracks.TryGetValue(key, out int handle))
            {
                float finalVol = Math.Clamp(volume, 0f, 1f) * _musicVolume * _masterVolume * 0.1f;
                float globalTrackerVolume = finalVol * 100;
                Bass.ChannelSetAttribute(handle, ChannelAttribute.MusicVolumeGlobal, globalTrackerVolume);
                Bass.ChannelSetAttribute(handle, ChannelAttribute.MusicVolumeChannel, globalTrackerVolume);
            }
        }

        private static void UpdateChannelVolume(int handle)
        {
            float finalVol = _musicVolume * _masterVolume * 0.1f;
            float globalTrackerVolume = finalVol * 100f;
            Bass.ChannelSetAttribute(handle, ChannelAttribute.MusicVolumeGlobal, globalTrackerVolume);
            Bass.ChannelSetAttribute(handle, ChannelAttribute.MusicVolumeChannel, globalTrackerVolume);
        }

        public static void SetMusicChannelVolume(string assetName, int trackIndex, int volume)
        {
            string key = assetName.ToUpper();
            if (_loadedMusicTracks.TryGetValue(key, out int hMusic))
            {
                int clampedVolume = Math.Clamp(volume, 0, 100);
                Bass.ChannelSetAttribute(hMusic, ChannelAttribute.MusicVolumeChannel + trackIndex, clampedVolume);
            }
        }

        public static void PlayMusicTrackWithOffset(string assetName, int rawOffset, bool restart = false)
        {
            string key = assetName.ToUpper();
            if (_loadedMusicTracks.TryGetValue(key, out int handle))
            {
                if (restart)
                {
                    Bass.ChannelStop(handle);
                    _activePlayingMusic.Remove(handle);
                }

                _activePlayingMusic.Add(handle);
                UpdateChannelVolume(handle);

                int orderIndex = rawOffset & 0xFFFF;
                Bass.ChannelSetPosition(handle, (long)orderIndex, PositionFlags.MusicOrders);
                Bass.ChannelPlay(handle, false);
            }
        }

        public static void PlayMusicTrack(string assetName, bool restart = false)
        {
            string key = assetName.ToUpper();
            if (!_loadedMusicTracks.TryGetValue(key, out int handle)) return;

            if (restart)
            {
                Bass.ChannelStop(handle);
                _activePlayingMusic.Remove(handle);
            }
            else if (_activePlayingMusic.Contains(handle) && Bass.ChannelIsActive(handle) == PlaybackState.Playing)
            {
                return;
            }

            _activePlayingMusic.Add(handle);
            UpdateChannelVolume(handle);
            Bass.ChannelPlay(handle, restart);
        }

        public static void StopMusicTrack(string assetName)
        {
            string key = assetName.ToUpper();
            if (_loadedMusicTracks.TryGetValue(key, out int handle))
            {
                Bass.ChannelStop(handle);
                _activePlayingMusic.Remove(handle);
            }
        }
        public static void StopAllMusic()
        {
            foreach (var handle in _activePlayingMusic) Bass.ChannelStop(handle);
            _activePlayingMusic.Clear();
        }
        public static void SetVolume(float master, float sfx, float music)
        {
            _masterVolume = Math.Clamp(master, 0f, 1f);
            _sfxVolume = Math.Clamp(sfx, 0f, 1f);
            _musicVolume = Math.Clamp(music, 0f, 1f);
            foreach (var handle in _activePlayingMusic) UpdateChannelVolume(handle);
        }
        #endregion
        public static void Shutdown()
        {
            StopAllMusic();
            foreach (var handle in _loadedMusicTracks.Values) Bass.MusicFree(handle);
            _loadedMusicTracks.Clear();
            if (_context.Handle != IntPtr.Zero)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(_context);
            }
            if (_device.Handle != IntPtr.Zero) ALC.CloseDevice(_device);
            if (_bassInitialized)
            {
                Bass.Free();
                _bassInitialized = false;
            }
        }
    }
}