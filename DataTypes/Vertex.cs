using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace KrutolFramework.DataTypes
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex2D
    {
        public Vector2 Position;
        public Vector2 TexCoords;
        public float TextureLayer; // <--- Добавить это поле
        public Color4 Color;
    }
}
