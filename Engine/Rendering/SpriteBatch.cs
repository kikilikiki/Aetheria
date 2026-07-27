using System.Numerics;
using Silk.NET.OpenGL;

namespace Aetheria.Engine.Rendering;

/// <summary>
/// Regroupe les rectangles texturés (sprites, tuiles de grille, surbrillances de case) en un
/// minimum d'appels de dessin. Usage : <c>Begin(camera)</c>, N x <c>Draw(...)</c>, <c>End()</c>.
/// Un changement de texture déclenche un flush — pas d'atlas multi-texture dans cette première
/// version, volontairement simple (voir <c>Docs/README.md</c> pour les évolutions prévues).
/// </summary>
public sealed unsafe class SpriteBatch : IDisposable
{
    private const int MaxQuadsPerBatch = 2000;
    private const int VerticesPerQuad = 4;
    private const int FloatsPerVertex = 8; // position(2) + uv(2) + couleur(4)

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTexCoord;
        layout (location = 2) in vec4 aColor;

        uniform mat4 uViewProjection;

        out vec2 vTexCoord;
        out vec4 vColor;

        void main()
        {
            gl_Position = uViewProjection * vec4(aPosition, 0.0, 1.0);
            vTexCoord = aTexCoord;
            vColor = aColor;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vTexCoord;
        in vec4 vColor;

        uniform sampler2D uTexture;

        out vec4 FragColor;

        void main()
        {
            FragColor = texture(uTexture, vTexCoord) * vColor;
        }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;

    private readonly float[] _vertexData = new float[MaxQuadsPerBatch * VerticesPerQuad * FloatsPerVertex];
    private int _quadCount;
    private Texture2D? _currentTexture;
    private Matrix4x4 _viewProjection = Matrix4x4.Identity;

    public SpriteBatch(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexShaderSource, FragmentShaderSource);

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer,
            (nuint)(_vertexData.Length * sizeof(float)), null, BufferUsageARB.DynamicDraw);

        var stride = (uint)(FloatsPerVertex * sizeof(float));
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(4 * sizeof(float)));

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        var indices = BuildIndices(MaxQuadsPerBatch);
        fixed (ushort* ptr = indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(ushort)), ptr, BufferUsageARB.StaticDraw);
        }

        _gl.BindVertexArray(0);
    }

    public void Begin(Camera2D camera)
    {
        _viewProjection = camera.GetViewProjection();
        _quadCount = 0;
        _currentTexture = null;
    }

    /// <summary>Dessine un rectangle texturé. <paramref name="color"/> module la texture (blanc = couleur d'origine).</summary>
    public void Draw(Texture2D texture, Vector2 position, Vector2 size, Vector4 color)
    {
        if (_currentTexture is not null && _currentTexture != texture)
        {
            Flush();
        }

        if (_quadCount == MaxQuadsPerBatch)
        {
            Flush();
        }

        _currentTexture = texture;

        Span<(float x, float y, float u, float v)> corners =
        [
            (position.X, position.Y, 0f, 0f),
            (position.X + size.X, position.Y, 1f, 0f),
            (position.X + size.X, position.Y + size.Y, 1f, 1f),
            (position.X, position.Y + size.Y, 0f, 1f),
        ];

        var vertexOffset = _quadCount * VerticesPerQuad * FloatsPerVertex;
        for (var i = 0; i < corners.Length; i++)
        {
            var (x, y, u, v) = corners[i];
            var o = vertexOffset + i * FloatsPerVertex;
            _vertexData[o + 0] = x;
            _vertexData[o + 1] = y;
            _vertexData[o + 2] = u;
            _vertexData[o + 3] = v;
            _vertexData[o + 4] = color.X;
            _vertexData[o + 5] = color.Y;
            _vertexData[o + 6] = color.Z;
            _vertexData[o + 7] = color.W;
        }

        _quadCount++;
    }

    public void End()
    {
        if (_quadCount > 0)
        {
            Flush();
        }
    }

    private void Flush()
    {
        if (_quadCount == 0 || _currentTexture is null)
        {
            _quadCount = 0;
            return;
        }

        _shader.Use();
        _shader.SetUniform("uViewProjection", _viewProjection);
        _shader.SetUniform("uTexture", 0);
        _currentTexture.Bind();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var vertexCount = _quadCount * VerticesPerQuad * FloatsPerVertex;
        fixed (float* ptr = _vertexData)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertexCount * sizeof(float)), ptr);
        }

        _gl.DrawElements(PrimitiveType.Triangles, (uint)(_quadCount * 6), DrawElementsType.UnsignedShort, null);

        _quadCount = 0;
    }

    private static ushort[] BuildIndices(int maxQuads)
    {
        var indices = new ushort[maxQuads * 6];
        for (var i = 0; i < maxQuads; i++)
        {
            var offset = i * 6;
            var baseVertex = (ushort)(i * VerticesPerQuad);
            indices[offset + 0] = baseVertex;
            indices[offset + 1] = (ushort)(baseVertex + 1);
            indices[offset + 2] = (ushort)(baseVertex + 2);
            indices[offset + 3] = (ushort)(baseVertex + 2);
            indices[offset + 4] = (ushort)(baseVertex + 3);
            indices[offset + 5] = baseVertex;
        }

        return indices;
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
