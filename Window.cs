
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;

namespace KrutolFramework.Core
{
    public class FrameworkGameWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : GameWindow(gameWindowSettings, nativeWindowSettings)
    {
        public static readonly Vector2i VirtualResolution = new(1920, 1080);
        public static float TargetAspectRatio => (float)VirtualResolution.X / VirtualResolution.Y;

        // Смещения и размеры актуального вьюпорта (пригодятся для перевода координат мыши)
        public static int ViewportX { get; private set; }
        public static int ViewportY { get; private set; }
        public static int ViewportWidth { get; private set; }
        public static int ViewportHeight { get; private set; }
        /// <summary>
        /// Вызывается один раз при инициализации окна. Здесь настраивается OpenGL.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();

            // Включаем вывод отладочных сообщений OpenGL 4.6 (полезно при разработке)
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);

            // Базовые настройки рендеринга
            GL.ClearColor(0.1f, 0.12f, 0.16f, 1.0f); // Цвет очистки экрана (тёмно-серый)
            GL.Enable(EnableCap.DepthTest);           // Включаем тест глубины для 3D

            Console.WriteLine($"[Framework] OpenGL Инициализирован.");
            Console.WriteLine($"[Framework] Видеокарта: {GL.GetString(StringName.Renderer)}");
            Console.WriteLine($"[Framework] Версия GL: {GL.GetString(StringName.Version)}");
        }

        /// <summary>
        /// Вызывается перед каждым кадром логики. Здесь обновляются физика, ввод и состояния объектов.
        /// </summary>
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            // Пример обработки ввода: закрытие на Escape
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }

            // TODO: Обновление вашей игровой логики (Update managers, ЕCS, etc.)
            // Передаем args.Time — время прошедшее с прошлого кадра (DeltaTime)
        }

        /// <summary>
        /// Вызывается перед каждым кадром отрисовки. Здесь происходит весь рендеринг.
        /// </summary>
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            // Очищаем буферы цвета и глубины
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // TODO: Отрисовка вашей сцены (Render managers, SpriteBatch, etc.)

            // Меняем буферы местами (Double Buffering)
            SwapBuffers();
        }



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            int width = e.Width;
            int height = e.Height;
            float windowAspectRatio = (float)width / height;

            if (windowAspectRatio > TargetAspectRatio)
            {
                // Окно слишком широкое: Pillarboxing (полосы по бокам)
                ViewportHeight = height;
                ViewportWidth = (int)(height * TargetAspectRatio);
                ViewportX = (width - ViewportWidth) / 2;
                ViewportY = 0;
            }
            else
            {
                // Окно слишком высокое: Letterboxing (полосы сверху и снизу)
                ViewportWidth = width;
                ViewportHeight = (int)(width / TargetAspectRatio);
                ViewportX = 0;
                ViewportY = (height - ViewportHeight) / 2;
            }

            // Устанавливаем вьюпорт в OpenGL
            OpenTK.Graphics.OpenGL4.GL.Viewport(ViewportX, ViewportY, ViewportWidth, ViewportHeight);
        }
        

        /// <summary>
        /// Вызывается при закрытии окна. Идеальное место для освобождения GPU ресурсов.
        /// </summary>
        protected override void OnUnload()
        {
            // TODO: Освободить шейдеры, VBO, VAO, текстуры
            Console.WriteLine("[Framework] Окно закрывается, ресурсы освобождены.");

            base.OnUnload();
        }

        /// <summary>
        /// Коллбэк для отлова ошибок OpenGL напрямую в консоль .NET
        /// </summary>
        private static void DebugCallback(DebugSource source, DebugType type, int id,
            DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            string msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length);

            // Игнорируем незначительные уведомления, выводим только важные предупреждения и ошибки
            if (severity != DebugSeverity.DebugSeverityNotification)
            {
                Console.WriteLine($"[OpenGL Error] [{severity}] {msg}");
            }
        }
    }
}


