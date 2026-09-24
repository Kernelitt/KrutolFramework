using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace KrutolFramework.DataTypes
{


    namespace MyGameFramework.Core
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vertex2D
        {
            public Vector2 Position;  // Сдвиг (X, Y)
            public Vector2 TexCoords; // UV координаты (X, Y)
            public Color4 Color;      // Цвет/Альфа (R, G, B, A)
        }
    }

}
