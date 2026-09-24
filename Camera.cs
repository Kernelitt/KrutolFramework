using OpenTK.Mathematics;

namespace KrutolFramework.Core
{


    public class Camera2D
    {
        public Vector2 Position { get; set; } = Vector2.Zero;
        public float Zoom { get; set; } = 1.0f;
        public float Rotation { get; set; } = 0.0f; // В градусах

        public Matrix4 GetViewMatrix()
        {
            // Сдвиг к позиции камеры (инвертированный, так как мы двигаем мир относительно камеры)
            Matrix4 translation = Matrix4.CreateTranslation(-Position.X, -Position.Y, 0.0f);
            Matrix4 rotation = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Rotation));
            Matrix4 scale = Matrix4.CreateScale(Zoom, Zoom, 1.0f);

            // В 2D камере важен порядок умножения матриц
            return translation * rotation * scale;
        }

        public Matrix4 GetProjectionMatrix()
        {
            // Всегда проецируем на наше фиксированное виртуальное пространство
            float halfWidth = FrameworkGameWindow.VirtualResolution.X / 2f;
            float halfHeight = FrameworkGameWindow.VirtualResolution.Y / 2f;

            // Камера центрирована по умолчанию (0,0 — центр экрана)
            return Matrix4.CreateOrthographicOffCenter(
                -halfWidth, halfWidth,
                halfHeight, -halfHeight,
                -1.0f, 1.0f
            );
        }

        public Matrix4 GetTotalMatrix()
        {
            return GetViewMatrix() * GetProjectionMatrix();
        }
    }
}


