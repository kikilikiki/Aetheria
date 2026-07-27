using Silk.NET.OpenGL;
using StbImageSharp;

namespace Aetheria.Engine.Rendering;

/// <summary>Texture 2D chargée en mémoire GPU. Filtrage "Nearest" par défaut (adapté au pixel art).</summary>
public sealed class Texture2D : IDisposable
{
    private readonly GL _gl;

    public uint Handle { get; }
    public int Width { get; }
    public int Height { get; }

    private unsafe Texture2D(GL gl, ReadOnlySpan<byte> pixelsRgba, int width, int height)
    {
        _gl = gl;
        Width = width;
        Height = height;

        Handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, Handle);

        fixed (byte* ptr = pixelsRgba)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
    }

    /// <summary>Charge un fichier image (PNG/JPG/...) via stb_image.</summary>
    public static Texture2D FromFile(GL gl, string path)
    {
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        return new Texture2D(gl, image.Data, image.Width, image.Height);
    }

    /// <summary>Texture 1x1 unie, pratique tant que les vraies illustrations (bestiaire, tuiles) n'existent pas.</summary>
    public static Texture2D CreateSolidColor(GL gl, byte r, byte g, byte b, byte a = 255)
        => new(gl, [r, g, b, a], 1, 1);

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}
