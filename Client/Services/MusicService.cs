using NAudio.Wave;

namespace Aetheria.Client.Services;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute les musiques (/music) chill.wav pour la musique en
/// ville et combat.wav pour les musiques en combat (elles doivent se répéter)" : une piste par
/// catégorie de scène, en boucle infinie (<see cref="LoopStream"/>), ne relance la lecture que
/// lorsque la catégorie change réellement (même pattern que DiscordPresenceService — évite de
/// redémarrer la piste à chaque frame). Fichiers attendus à côté de l'exécutable, dans
/// Music/&lt;nom&gt;.wav (voir Aetheria.Client.csproj — copiés depuis /Music à la racine du dépôt).
/// </summary>
public sealed class MusicService : IDisposable
{
    public enum Track
    {
        None,
        Town,
        Combat,
    }

    private readonly string _musicDirectory;
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private Track _currentTrack = Track.None;
    private float _volume = 1f;

    public MusicService()
    {
        _musicDirectory = Path.Combine(AppContext.BaseDirectory, "Music");
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajoute dans les options un paramètre de volume de la
    /// musique" : appliqué immédiatement à la piste en cours (pas besoin de relancer la lecture),
    /// et mémorisé pour la prochaine piste lancée par <see cref="Update"/>.
    /// </summary>
    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        if (_output is not null)
        {
            _output.Volume = _volume;
        }
    }

    /// <summary>À appeler à chaque frame avec la catégorie de musique désirée pour la scène active.</summary>
    public void Update(Track track)
    {
        if (track == _currentTrack)
        {
            return;
        }

        _currentTrack = track;

        var fileName = track switch
        {
            Track.Town => "chill.wav",
            Track.Combat => "combat.wav",
            _ => null,
        };

        Stop();

        // NAudio s'appuie sur les API audio Windows (winmm/DirectSound) : indisponible sur Linux
        // (voir Sites/README.md — le Client se compile aussi en linux-x64), le jeu tourne alors
        // simplement sans musique plutôt que de planter au chargement d'une DLL native absente.
        if (fileName is null || !OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_musicDirectory, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            _reader = new AudioFileReader(path);
            _output = new WaveOutEvent { Volume = _volume };
            _output.Init(new LoopStream(_reader));
            _output.Play();
        }
        catch (Exception)
        {
            // Pas de sortie audio disponible (ex. session distante sans périphérique son) :
            // le jeu continue silencieusement plutôt que de planter.
            Stop();
        }
    }

    private void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _reader?.Dispose();
        _reader = null;
    }

    public void Dispose() => Stop();
}

/// <summary>Enveloppe un <see cref="WaveStream"/> pour le rejouer indéfiniment (voir doc NAudio — pattern standard de boucle).</summary>
internal sealed class LoopStream(WaveStream source) : WaveStream
{
    public override WaveFormat WaveFormat => source.WaveFormat;
    public override long Length => source.Length;
    public override long Position
    {
        get => source.Position;
        set => source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = source.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
            {
                if (source.Position == 0)
                {
                    break;
                }

                source.Position = 0;
                continue;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
