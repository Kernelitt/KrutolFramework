using OpenTK.Mathematics;

namespace KrutolFramework.Core
{ 


    public abstract class UIComponent
    {
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public bool IsVisible { get; set; } = true;

        // Метод проверки попадания мыши в прямоугольник (AABB)
        public bool IsMouseOver()
        {
            Vector2 mouse = Input.MousePosition;
            return mouse.X >= Position.X && mouse.X <= Position.X + Size.X &&
                   mouse.Y >= Position.Y && mouse.Y <= Position.Y + Size.Y;
        }

        // Попиксельный расчет коллизии по альфа-каналу текстуры
        public bool IsMouseOverPixelPerfect(TextureRegion region)
        {
            if (!IsMouseOver() || region.RawRgbaData == null) return false;

            Vector2 mouse = Input.MousePosition;
            // Переводим экранные координаты мыши в локальные пиксели текстуры
            int localX = (int)((mouse.X - Position.X) / Size.X * region.Width);
            int localY = (int)((mouse.Y - Position.Y) / Size.Y * region.Height);

            localX = Math.Clamp(localX, 0, region.Width - 1);
            localY = Math.Clamp(localY, 0, region.Height - 1);

            // Индекс байта альфа-канала (RGBA: R=0, G=1, B=2, A=3)
            int pixelIndex = (localY * region.Width + localX) * 4;
            byte alpha = region.RawRgbaData[pixelIndex + 3];

            return alpha > 10; // Считаем попаданием, если пиксель не прозрачный (альфа > 10)
        }

        public abstract void Update(float deltaTime);
        public abstract void Render(SpriteBatch batch);
    }
}
