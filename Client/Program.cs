using System.Text;
using Aetheria.Engine.Core;
using Aetheria.Shared;
using Silk.NET.OpenGL;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"{GameInfo.Name} Client v{GameInfo.Version}");

using var host = new GameHost($"{GameInfo.Name} — v{GameInfo.Version}", 1280, 720);

host.Load += () =>
{
    Console.WriteLine("Moteur initialisé : fenêtre + contexte OpenGL prêts.");
};

host.Render += _ =>
{
    host.Gl.ClearColor(0.08f, 0.09f, 0.12f, 1.0f);
    host.Gl.Clear(ClearBufferMask.ColorBufferBit);
};

host.Run();
