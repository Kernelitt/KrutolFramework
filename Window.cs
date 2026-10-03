
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace KrutolFramework.Core
{
    public class FrameworkGameWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : GameWindow(gameWindowSettings, nativeWindowSettings)
    {
        public static readonly Vector2i VirtualResolution = new(1600, 900);

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);

            // Базовые настройки рендеринга
            GL.ClearColor(0.1f, 0.12f, 0.16f, 1.0f);
            GL.Disable(EnableCap.DepthTest);          

            Console.WriteLine($"[Framework] OpenGL Инициализирован.");
            Console.WriteLine($"[Framework] Видеокарта: {GL.GetString(StringName.Renderer)}");
            Console.WriteLine($"[Framework] Версия GL: {GL.GetString(StringName.Version)}");
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            // Очищаем буферы цвета и глубины
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
        }
        

        protected override void OnUnload()
        {
            base.OnUnload();
        }

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


