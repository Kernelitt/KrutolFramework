using KrutolFramework.Core;
using KrutolFramework.DataTypes.MyGameFramework.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Runtime.InteropServices;


namespace KrutolFramework
{
        public class SpriteBatch : IDisposable
        {
            private const int MAX_SPRITES = 10000; // Сколько спрайтов можем отрисовать за один вызов (вызов Draw)
            private const int VERTICES_PER_SPRITE = 4;
            private const int INDICES_PER_SPRITE = 6;
            private const int MAX_VERTEX_COUNT = MAX_SPRITES * VERTICES_PER_SPRITE;

            private readonly int _vao;
            private readonly int _vbo;
            private readonly int _ebo;

            private readonly Vertex2D[] _vertexArray = new Vertex2D[MAX_VERTEX_COUNT];
            private int _spriteCount = 0;
            private int _currentTextureHandle = 0;

            private Shader _shader; // Базовый шейдер (код ниже)

            public SpriteBatch()
            {
                // 1. Создаем буфер индексов (он статичен, так как топология квадов не меняется)
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

                // Выделяем память под динамический VBO вершины
                GL.NamedBufferData(_vbo, MAX_VERTEX_COUNT * Marshal.SizeOf<Vertex2D>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);

                // Загружаем статичные индексы в EBO
                GL.NamedBufferData(_ebo, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

                // Настройка связывания VAO -> VBO/EBO
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

                // Атрибут 2: Цвет (Color4)
                GL.EnableVertexArrayAttrib(_vao, 2);
                GL.VertexArrayAttribFormat(_vao, 2, 4, VertexAttribType.Float, false, Marshal.OffsetOf<Vertex2D>("Color").ToInt32());
                GL.VertexArrayAttribBinding(_vao, 2, 0);

                // Создаем стандартный спрайтовый шейдер
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
                // Если сменилась текстура атласа или буфер забит — сбрасываем на GPU
                if (_spriteCount >= MAX_SPRITES || (_currentTextureHandle != 0 && _currentTextureHandle != region.AtlasTextureHandle))
                {
                    Flush();
                }

                _currentTextureHandle = region.AtlasTextureHandle;

                // Вычисляем локальные размеры с учетом масштаба
                float w = region.Width * scale.X;
                float h = region.Height * scale.Y;

                // Точка вращения (центр спрайта)
                float originX = w * 0.5f;
                float originY = h * 0.5f;

                // Локальные координаты углов квада относительно центра
                float x0 = -originX, y0 = -originY;
                float x1 = w - originX, y1 = -originY;
                float x2 = w - originX, y2 = h - originY;
                float x3 = -originX, y3 = h - originY;

                // Если есть поворот, трансформируем вершины на CPU (быстрее, чем куча матриц на GPU)
                if (rotationDegrees != 0.0f)
                {
                    float radians = MathHelper.DegreesToRadians(rotationDegrees);
                    float cos = MathF.Cos(radians);
                    float sin = MathF.Sin(radians);

                    // Функция поворота вектора
                    void Rotate(ref float x, ref float y)
                    {
                        float rx = x * cos - y * sin;
                        float ry = x * sin + y * cos;
                        x = rx + position.X + originX;
                        y = ry + position.Y + originY;
                    }

                    Rotate(ref x0, ref y0);
                    Rotate(ref x1, ref y1);
                    Rotate(ref x2, ref y2);
                    Rotate(ref x3, ref y3);
                }
                else
                {
                    // Если поворота нет, просто смещаем на позицию мировых координат
                    x0 += position.X + originX; y0 += position.Y + originY;
                    x1 += position.X + originX; y1 += position.Y + originY;
                    x2 += position.X + originX; y2 += position.Y + originY;
                    x3 += position.X + originX; y3 += position.Y + originY;
                }

                int index = _spriteCount * VERTICES_PER_SPRITE;

                // Заполняем массив вершин данными спрайта
                _vertexArray[index + 0] = new Vertex2D { Position = new Vector2(x0, y0), TexCoords = new Vector2(region.U1, region.V1), Color = color };
                _vertexArray[index + 1] = new Vertex2D { Position = new Vector2(x1, y1), TexCoords = new Vector2(region.U2, region.V1), Color = color };
                _vertexArray[index + 2] = new Vertex2D { Position = new Vector2(x2, y2), TexCoords = new Vector2(region.U2, region.V2), Color = color };
                _vertexArray[index + 3] = new Vertex2D { Position = new Vector2(x3, y3), TexCoords = new Vector2(region.U1, region.V2), Color = color };

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

                // Биндим текстуру текущего атласа в юнит 0 (OpenGL 4.6 DSA style)
                GL.BindTextureUnit(0, _currentTextureHandle);

                // Копируем данные из RAM массива в VBO на видеокарте
                int verticesToUpload = _spriteCount * VERTICES_PER_SPRITE;
                GL.NamedBufferSubData(_vbo, IntPtr.Zero, verticesToUpload * Marshal.SizeOf<Vertex2D>(), _vertexArray);

                // Отрисовка
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

            #region Шейдерный код
            private const string VertexShaderSource = @"
        #version 460 core
        layout(location = 0) in vec2 aPosition;
        layout(location = 1) in vec2 aTexCoords;
        layout(location = 2) in vec4 aColor;

        out vec2 vTexCoords;
        out vec4 vColor;

        uniform mat4 uProjection;

        void main() {
            vTexCoords = aTexCoords;
            vColor = aColor;
            gl_Position = uProjection * vec4(aPosition, 0.0, 1.0);
        }";

            private const string FragmentShaderSource = @"
        #version 460 core
        in vec2 vTexCoords;
        in vec4 vColor;

        out vec4 fColor;

        layout(binding = 0) uniform sampler2D uTexture;

        void main() {
            fColor = texture(uTexture, vTexCoords) * vColor;
        }";
            #endregion
        }

        // Вспомогательный минимальный класс Шейдера
        public class Shader : IDisposable
        {
            public int Handle { get; private set; }
            public Shader(string vertexSource, string fragmentSource)
            {
                int vs = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(vs, vertexSource);
                GL.CompileShader(vs);

                int fs = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(fs, fragmentSource);
                GL.CompileShader(fs);

                Handle = GL.CreateProgram();
                GL.AttachShader(Handle, vs);
                GL.AttachShader(Handle, fs);
                GL.LinkProgram(Handle);

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
            public void Dispose() => GL.DeleteProgram(Handle);
        }
    }

