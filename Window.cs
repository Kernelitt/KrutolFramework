
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
        public static readonly Vector2i VirtualResolution = new(1600, 900);

        protected override void OnLoad()
        {
            base.OnLoad();

            // Включаем вывод отладочных сообщений OpenGL 4.6 (полезно при разработке)
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);

            // Базовые настройки рендеринга
            GL.ClearColor(0.1f, 0.12f, 0.16f, 1.0f); // Цвет очистки экрана (тёмно-серый)
            GL.Disable(EnableCap.DepthTest);           // Включаем тест глубины для 3D

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
        }

        /// <summary>
        /// Вызывается перед каждым кадром отрисовки. Здесь происходит весь рендеринг.
        /// </summary>
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            
        }



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
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


