using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrutolFramework.Core
{
    public static class Input
    {
        private static KeyboardState _keyboard;
        private static MouseState _mouse;

        private static readonly bool[] _prevKeys = new bool[(int)Keys.LastKey + 1];
        private static readonly bool[] _currKeys = new bool[(int)Keys.LastKey + 1];

        public static Vector2 MousePosition { get; private set; }

        public static void Initialize(GameWindow window)
        {
            _keyboard = window.KeyboardState;
            _mouse = window.MouseState;
        }

        public static void Update(GameWindow window)
        {
            // Копируем состояние клавиатуры предыдущего кадра
            System.Array.Copy(_currKeys, _prevKeys, _currKeys.Length);

            // Опрашиваем текущее состояние
            for (int i = 0; i < _currKeys.Length; i++)
            {
                _currKeys[i] = _keyboard.IsKeyDown((Keys)i);
            }

            // Перевод координат мыши с учетом Letterbox/Pillarbox в виртуальные 1920x1080
            float screenMouseX = _mouse.X - FrameworkGameWindow.ViewportX;
            float screenMouseY = _mouse.Y - FrameworkGameWindow.ViewportY;

            float normX = screenMouseX / FrameworkGameWindow.ViewportWidth;
            float normY = screenMouseY / FrameworkGameWindow.ViewportHeight;

            MousePosition = new Vector2(
                normX * FrameworkGameWindow.VirtualResolution.X,
                normY * FrameworkGameWindow.VirtualResolution.Y
            );
        }

        public static bool IsKeyDown(Keys key) => _currKeys[(int)key];
        public static bool IsKeyPressed(Keys key) => _currKeys[(int)key] && !_prevKeys[(int)key];
        public static bool IsKeyReleased(Keys key) => !_currKeys[(int)key] && _prevKeys[(int)key];
        public static bool IsMouseButtonDown(MouseButton button) => _mouse.IsButtonDown(button);
    }
}
