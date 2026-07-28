using System.Runtime.InteropServices;

namespace Aetheria.Shared.Settings;

/// <summary>
/// Détection de la disposition clavier du système (voir GDD — "détecté automatiquement et peut
/// être modifié"). Note : les codes de touche Silk.NET/GLFW (<c>Key.W</c>, <c>Key.A</c>, ...)
/// correspondent à la position physique de la touche, pas au caractère imprimé dessus — un
/// déplacement "WASD" fonctionne donc déjà nativement en ZQSD sur un clavier AZERTY sans aucun
/// remapping. Cette détection sert uniquement à choisir quel libellé afficher à l'écran
/// (ex. "ZQSD" plutôt que "WASD") et à la saisie de texte, qui elle utilise le caractère réel
/// (voir Engine/Input/KeyboardState.DrainTypedChars, lui bien dépendant de la disposition).
/// </summary>
public static class KeyboardLayoutResolver
{
    // Identifiants de langue (LANGID, partie basse du HKL) des dispositions AZERTY connues :
    // français de France et de Belgique. Le français canadien (0x0C0C) utilise un clavier
    // CSA/QWERTY, volontairement exclu.
    private static readonly HashSet<int> AzertyLanguageIds = [0x040C, 0x080C];

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    public static bool IsAzertyDetected()
    {
        try
        {
            var handle = GetKeyboardLayout(0);
            var languageId = (int)((long)handle & 0xFFFF);
            return AzertyLanguageIds.Contains(languageId);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool ShouldUseAzerty(KeyboardLayoutPreference preference) => preference switch
    {
        KeyboardLayoutPreference.Azerty => true,
        KeyboardLayoutPreference.Qwerty => false,
        _ => IsAzertyDetected(),
    };
}
