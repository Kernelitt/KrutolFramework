using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace KrutolFramework.Core
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex2D
    {
        public Vector2 Position;     // Координаты X, Y
        public Vector2 TexCoords;    // UV координаты U, V
        public float TextureLayer;   // ИНДЕКС СЛОЯ в Texture2DArray (передается как float на GPU)
        public Color4 Color;         // Цвет/Альфа
    }

    public class SpriteBatch : IDisposable
    {
        private const int MAX_SPRITES = 10000;
        private const int VERTICES_PER_SPRITE = 4;
        private const int INDICES_PER_SPRITE = 6;
        private const int MAX_VERTEX_COUNT = MAX_SPRITES * VERTICES_PER_SPRITE;

        private readonly int _vao;
        private readonly int _vbo;
        private readonly int _ebo;

        private readonly Vertex2D[] _vertexArray = new Vertex2D[MAX_VERTEX_COUNT];
        private int _spriteCount = 0;
        private int _currentTextureHandle = 0;

        private readonly Shader _shader;

        public SpriteBatch()
        {
            // 1. Создаем буфер индексов (топология квадов неизменна)
            int[] indices = new int[MAX_SPRITES * INDICES_PER_SPRITE];
            int vertexOffset = 0;
            for (int i = 0; i < indices.Length; i += INDICES_PER_SPRITE)
            {
                indices[i + 0] = vertexOffset + 0;
                indices[i + 1] = vertexOffset + 1;
                indices[i + 2] = vertexOffset + 2;
                indices[i + 3] = vertexOffset + 2;
                indices[i + 4] = vertexOffset + 3;
                indices[i + 5] = vertexOffset + 0;
                vertexOffset += VERTICES_PER_SPRITE;
            }

            // 2. Инициализация буферов через OpenGL 4.6 DSA
            GL.CreateVertexArrays(1, out _vao);
            GL.CreateBuffers(1, out _vbo);
            GL.CreateBuffers(1, out _ebo);

            GL.NamedBufferData(_vbo, MAX_VERTEX_COUNT * Marshal.SizeOf<Vertex2D>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.NamedBufferData(_ebo, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

            GL.VertexArrayVertexBuffer(_vao, 0, _vbo, IntPtr.Zero, Marshal.SizeOf<Vertex2D>());
            GL.VertexArrayElementBuffer(_vao, _ebo);

            // Атрибут 0: Позиция (Vector2)
            GL.EnableVertexArrayAttrib(_vao, 0);
            GL.VertexArrayAttribFormat(_vao, 0, 2, VertexAttribType.Float, false, 0);
            GL.VertexArrayAttribBinding(_vao, 0, 0);

            // Атрибут 1: UV-Координаты (Vector2)
            GL.EnableVertexArrayAttrib(_vao, 1);
            GL.VertexArrayAttribFormat(_vao, 1, 2, VertexAttribType.Float, false, Marshal.OffsetOf<Vertex2D>("TexCoords").ToInt32());
            GL.VertexArrayAttribBinding(_vao, 1, 0);

            // Атрибут 2: Индекс слоя (float) <-- ДОБАВЛЕНО ДЛЯ TEXTURE2DARRAY
            GL.EnableVertexArrayAttrib(_vao, 2);
            GL.VertexArrayAttribFormat(_vao, 2, 1, VertexAttribType.Float, false, Marshal.OffsetOf<Vertex2D>("TextureLayer").ToInt32());
            GL.VertexArrayAttribBinding(_vao, 2, 0);

            // Атрибут 3: Цвет (Color4)
            GL.EnableVertexArrayAttrib(_vao, 3);
            GL.VertexArrayAttribFormat(_vao, 3, 4, VertexAttribType.Float, false, Marshal.OffsetOf<Vertex2D>("Color").ToInt32());
            GL.VertexArrayAttribBinding(_vao, 3, 0);

            _shader = new Shader(VertexShaderSource, FragmentShaderSource);
        }

        public void Begin(Matrix4 projectionMatrix)
        {
            _shader.Use();
            _shader.SetMatrix4("uProjection", projectionMatrix);
            _spriteCount = 0;
            _currentTextureHandle = 0;
        }

        public void Draw(TextureRegion region, Vector2 position, Vector2 scale, float rotationDegrees, Color4 color)
        {
            // Если сменился хэндл объекта Texture2DArray или буфер заполнен — сбрасываем данные на GPU
            if (_spriteCount >= MAX_SPRITES || (_currentTextureHandle != 0 && _currentTextureHandle != region.AtlasTextureHandle))
            {
                Flush();
            }

            _currentTextureHandle = region.AtlasTextureHandle;

            float w = region.Width * scale.X;
            float h = region.Height * scale.Y;

            float originX = w * 0.5f;
            float originY = h * 0.5f;

            float x0 = -originX, y0 = -originY;
            float x1 = w - originX, y1 = -originY;
            float x2 = w - originX, y2 = h - originY;
            float x3 = -originX, y3 = h - originY;

            if (rotationDegrees != 0.0f)
            {
                float radians = MathHelper.DegreesToRadians(rotationDegrees);
                float cos = MathF.Cos(radians);
                float sin = MathF.Sin(radians);

                void Rotate(ref float x, ref float y)
                {
                    float rx = x * cos - y * sin;
                    float ry = x * sin + y * cos;
                    x = rx + position.X;
                    y = ry + position.Y;
                }

                Rotate(ref x0, ref y0);
                Rotate(ref x1, ref y1);
                Rotate(ref x2, ref y2);
                Rotate(ref x3, ref y3);
            }
            else
            {
                x0 += position.X; y0 += position.Y;
                x1 += position.X; y1 += position.Y;
                x2 += position.X; y2 += position.Y;
                x3 += position.X; y3 += position.Y;
            }

            int index = _spriteCount * VERTICES_PER_SPRITE;
            float layer = region.Layer; // Сохраняем слой

            // Заполняем массив вершин, передавая индекс слоя во все 4 вершины спрайта
            _vertexArray[index + 0] = new Vertex2D { Position = new Vector2(x0, y0), TexCoords = new Vector2(region.U1, region.V1), TextureLayer = layer, Color = color };
            _vertexArray[index + 1] = new Vertex2D { Position = new Vector2(x1, y1), TexCoords = new Vector2(region.U2, region.V1), TextureLayer = layer, Color = color };
            _vertexArray[index + 2] = new Vertex2D { Position = new Vector2(x2, y2), TexCoords = new Vector2(region.U2, region.V2), TextureLayer = layer, Color = color };
            _vertexArray[index + 3] = new Vertex2D { Position = new Vector2(x3, y3), TexCoords = new Vector2(region.U1, region.V2), TextureLayer = layer, Color = color };

            _spriteCount++;
        }

        // Добавьте в KrutolFramework.Core.SpriteBatch
        public void DrawDirectMatrix(TextureRegion region, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color4 color)
        {
            if (_spriteCount >= MAX_SPRITES || (_currentTextureHandle != 0 && _currentTextureHandle != region.AtlasTextureHandle))
            {
                Flush();
            }

            _currentTextureHandle = region.AtlasTextureHandle;
            int index = _spriteCount * VERTICES_PER_SPRITE;
            float layer = region.Layer;

            // Передаем вершины, которые мы уже спроецировали через матрицу трека на CPU
            _vertexArray[index + 0] = new Vertex2D { Position = v0, TexCoords = new Vector2(region.U1, region.V1), TextureLayer = layer, Color = color };
            _vertexArray[index + 1] = new Vertex2D { Position = v1, TexCoords = new Vector2(region.U2, region.V1), TextureLayer = layer, Color = color };
            _vertexArray[index + 2] = new Vertex2D { Position = v2, TexCoords = new Vector2(region.U2, region.V2), TextureLayer = layer, Color = color };
            _vertexArray[index + 3] = new Vertex2D { Position = v3, TexCoords = new Vector2(region.U1, region.V2), TextureLayer = layer, Color = color };

            _spriteCount++;
        }

        public void End()
        {
            if (_spriteCount > 0)
            {
                Flush();
            }
        }

        private void Flush()
        {
            if (_spriteCount == 0 || _currentTextureHandle == 0) return;

            // Биндим Texture2DArray вместо классического Texture2D
            GL.BindTexture(TextureTarget.Texture2DArray, _currentTextureHandle);

            int verticesToUpload = _spriteCount * VERTICES_PER_SPRITE;
            GL.NamedBufferSubData(_vbo, IntPtr.Zero, verticesToUpload * Marshal.SizeOf<Vertex2D>(), _vertexArray);

            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _spriteCount * INDICES_PER_SPRITE, DrawElementsType.UnsignedInt, 0);

            _spriteCount = 0;
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            _shader.Dispose();
        }

        #region Шейдерный код под sampler2DArray
        private const string VertexShaderSource =
            "#version 460 core\n" +
            "layout(location = 0) in vec2 aPosition;\n" +
            "layout(location = 1) in vec2 aTexCoords;\n" +
            "layout(location = 2) in float aTexLayer;\n" +
            "layout(location = 3) in vec4 aColor;\n" +
            "out vec2 vTexCoords;\n" +
            "out float vTexLayer;\n" +
            "out vec4 vColor;\n" +
            "uniform mat4 uProjection;\n" +
            "void main() {\n" +
            "    vTexCoords = aTexCoords;\n" +
            "    vTexLayer = aTexLayer;\n" +
            "    vColor = aColor;\n" +
            "    gl_Position = uProjection * vec4(aPosition, 0.0, 1.0);\n" +
            "}\n";

        private const string FragmentShaderSource =
            "#version 460 core\n" +
            "in vec2 vTexCoords;\n" +
            "in float vTexLayer;\n" +
            "in vec4 vColor;\n" +
            "out vec4 fColor;\n" +
            "layout(binding = 0) uniform sampler2DArray uTexture;\n" +
            "void main() {\n" +
            "    fColor = texture(uTexture, vec3(vTexCoords, vTexLayer)) * vColor;\n" +
            "}\n";
        #endregion

    }

    public class Shader : IDisposable
    {
        public int Handle { get; private set; }

        public Shader(string vertexSource, string fragmentSource)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertexSource);
            GL.CompileShader(vs);
            CheckShaderCompileStatus(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentSource);
            GL.CompileShader(fs);
            CheckShaderCompileStatus(fs);

            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vs);
            GL.AttachShader(Handle, fs);
            GL.LinkProgram(Handle);
            CheckProgramLinkStatus(Handle);

            GL.DetachShader(Handle, vs);
            GL.DetachShader(Handle, fs);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
        }

        public void Use() => GL.UseProgram(Handle);

        public void SetMatrix4(string name, Matrix4 matrix)
        {
            int loc = GL.GetUniformLocation(Handle, name);
            GL.UniformMatrix4(loc, false, ref matrix);
        }

        private static void CheckShaderCompileStatus(int shader)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
            if (status == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"[Shader Compile Error] {infoLog}");
            }
        }

        private static void CheckProgramLinkStatus(int program)
        { 
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
            
            if (status == 0) { 
                string infoLog = GL.GetProgramInfoLog(program); 
                throw new Exception($"[Shader Link Error] {infoLog}"); 
            } 
        }
        public void Dispose() 
        { 
            if (Handle != 0) 
            { 
                GL.DeleteProgram(Handle); 
                Handle = 0; 
            } 
        }
    }
}
