using System.Numerics;
using Silk.NET.OpenGL;

namespace Aetheria.Engine.Rendering;

/// <summary>Programme shader OpenGL (vertex + fragment) compilé et lié.</summary>
public sealed class Shader : IDisposable
{
    private readonly GL _gl;

    public uint Handle { get; }

    public Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        var vertex = Compile(ShaderType.VertexShader, vertexSource);
        var fragment = Compile(ShaderType.FragmentShader, fragmentSource);

        Handle = _gl.CreateProgram();
        _gl.AttachShader(Handle, vertex);
        _gl.AttachShader(Handle, fragment);
        _gl.LinkProgram(Handle);

        _gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var linkStatus);
        if (linkStatus == 0)
        {
            var infoLog = _gl.GetProgramInfoLog(Handle);
            throw new InvalidOperationException($"Échec de la liaison du shader : {infoLog}");
        }

        _gl.DetachShader(Handle, vertex);
        _gl.DetachShader(Handle, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
    }

    public void Use() => _gl.UseProgram(Handle);

    public void SetUniform(string name, int value)
    {
        var location = _gl.GetUniformLocation(Handle, name);
        _gl.Uniform1(location, value);
    }

    public unsafe void SetUniform(string name, Matrix4x4 matrix)
    {
        var location = _gl.GetUniformLocation(Handle, name);
        _gl.UniformMatrix4(location, 1, false, (float*)&matrix);
    }

    private uint Compile(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compileStatus);
        if (compileStatus == 0)
        {
            var infoLog = _gl.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"Échec de la compilation du shader {type} : {infoLog}");
        }

        return shader;
    }

    public void Dispose() => _gl.DeleteProgram(Handle);
}
