using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Aetheria.Launcher.Services;

/// <summary>
/// Voir Docs/Idees.md — vraie image de profil : sélection d'un fichier image locale (bouton
/// "Changer d'avatar"). Même pattern que <see cref="ClipboardService"/> — Avalonia expose le
/// sélecteur de fichiers via le <c>TopLevel</c> (fenêtre), MainWindow enregistre la référence à
/// sa construction pour que le ViewModel (sans référence à la vue, MVVM) puisse y accéder.
/// </summary>
public static class FilePickerService
{
    public static Window? MainWindow { get; set; }

    /// <summary>Retourne les octets + le nom du fichier choisi, ou <c>null</c> si l'utilisateur annule.</summary>
    public static async Task<(byte[] Bytes, string FileName)?> PickImageAsync()
    {
        if (MainWindow?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choisir une image de profil",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }],
        });

        if (files.Count == 0)
        {
            return null;
        }

        await using var stream = await files[0].OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return (memory.ToArray(), files[0].Name);
    }
}
