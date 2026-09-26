using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrutolFramework.Core
{
    public static class Input
    {
        private static MouseState _mouseState;
        private static KeyboardState _keyboardState;

        private static GameWindow _window;
        // Переменные для отслеживания триггера клика (нажато именно в этом кадре)
        private static bool _prevLeftButton = false;
        private static bool _currLeftButton = false;

        /// <summary>
        /// Текущие координаты мыши в обычных пикселях окна приложения.
        /// </summary>
        public static Vector2 MousePosition { get; private set; }

        /// <summary>
        /// Инициализация менеджера ввода. Вызывается один раз при старте игры.
        /// </summary>
        public static readonly Vector2 VirtualResolution = new Vector2(1600f, 900f);
        public static Vector2 VirtualMousePosition
        {
            get
            {
                if (_window == null) return Vector2.Zero;

                // 1. Получаем чистые координаты мыши от GLFW (в пикселях окна)
                float rawMouseX = _window.MouseState.X;
                float rawMouseY = _window.MouseState.Y;

                // 2. Получаем текущие физические размеры клиентской области окна
                float windowWidth = _window.ClientSize.X;
                float windowHeight = _window.ClientSize.Y;

                if (windowWidth <= 0 || windowHeight <= 0) return Vector2.Zero;

                // 3. Вычисляем коэффициенты масштабирования
                float scaleX = VirtualResolution.X / windowWidth;
                float scaleY = VirtualResolution.Y / windowHeight;

                // 4. Транслируем координаты в игровое пространство
                float virtualX = rawMouseX * scaleX;
                float virtualY = rawMouseY * scaleY;

                return new Vector2(virtualX, virtualY);
            }
        }
        public static void Initialize(GameWindow window)
        {
            _window = window;
            _mouseState = window.MouseState;
            _keyboardState = window.KeyboardState;
        }

        /// <summary>
        /// Обновление состояний ввода. Должно вызываться в самом начале OnUpdateFrame.
        /// </summary>
        public static void Update()
        {
            if (_mouseState == null) return;

            // Запоминаем, что было в прошлом кадре, и считываем текущий кадр
            _prevLeftButton = _currLeftButton;
            _currLeftButton = _mouseState.IsButtonDown(MouseButton.Left);

            // Считываем позицию мыши напрямую из окна (X и Y)
            MousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        }

        /// <summary>
        /// Проверка: удерживается ли левая кнопка мыши прямо сейчас.
        /// </summary>
        public static bool IsMouseButtonDown(MouseButton button)
            {
            return _mouseState?.IsButtonDown(button) ?? false;
            }

        /// <summary>
        /// Проверка: была ли левая кнопка мыши НАЖАТА именно в текущем кадре.
        /// (Возвращает true только один раз за клик, предотвращая спам).
        /// </summary>
        public static bool IsMouseButtonPressed(MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                return _currLeftButton && !_prevLeftButton;
            }
            return _mouseState?.IsButtonDown(button) ?? false;
        }

        /// <summary>
        /// Проверка удержания клавиши на клавиатуре.
        /// </summary>
        public static bool IsKeyDown(Keys key)
        {
            return _keyboardState?.IsKeyDown(key) ?? false;
        }
    }
}
