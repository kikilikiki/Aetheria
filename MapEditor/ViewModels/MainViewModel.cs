using System.Collections.ObjectModel;
using System.Windows.Media;
using Aetheria.MapEditor.Services;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.MapEditor.ViewModels;

/// <summary>Voir Docs/Idees.md — rendu visuel de grille : une salle positionnée pour un <c>Canvas</c> (voir MainWindow.xaml), couleur selon le type de rencontre.</summary>
public sealed record RoomVisual(int Index, string EncounterType, double X, double Y, double Size, Brush Fill);

/// <summary>Voir Docs/Idees.md — connecteur de porte entre deux salles adjacentes (voir <see cref="MainViewModel.PreviewFloor"/>).</summary>
public sealed record DoorLineVisual(double X1, double Y1, double X2, double Y2);

/// <summary>
/// Édition du catalogue de donjons (voir <c>Docs/GameDesign.md</c> — section Donjons) : liste
/// des donjons à gauche, formulaire d'édition à droite, et prévisualisation de la génération
/// procédurale d'un étage — liste textuelle ET rendu visuel de grille (voir Docs/Idees.md,
/// <see cref="RoomVisuals"/>/<see cref="DoorVisuals"/>, à partir de
/// <c>DungeonRoom.GridX</c>/<c>GridY</c>/portes déjà calculés côté serveur mais jusqu'ici jetés).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DungeonApiClient _api = new();

    public ObservableCollection<DungeonData> Dungeons { get; } = [];
    public ObservableCollection<KingdomData> Kingdoms { get; } = [];
    public ObservableCollection<string> PreviewRooms { get; } = [];
    public ObservableCollection<RoomVisual> RoomVisuals { get; } = [];
    public ObservableCollection<DoorLineVisual> DoorVisuals { get; } = [];

    [ObservableProperty]
    private DungeonData? _selectedDungeon;

    [ObservableProperty]
    private KingdomData? _selectedKingdom;

    [ObservableProperty]
    private int? _editingId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private int _seed = 1000;

    [ObservableProperty]
    private int _previewFloorNumber = 1;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    // Voir Docs/Idees.md — authentification admin dédiée (jusqu'ici cet outil était supposé
    // lancé uniquement contre un serveur de confiance, sans aucune vérification).
    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _adminUsernameOrEmail = string.Empty;

    /// <summary>Voir AdminPanel — jamais lié directement en XAML (un PasswordBox ne supporte pas le data binding de son mot de passe pour des raisons de sécurité), assigné depuis le code-behind sur PasswordChanged.</summary>
    public string AdminPassword { get; set; } = string.Empty;

    [ObservableProperty]
    private string? _loginErrorMessage;

    private string? _sessionToken;

    [RelayCommand]
    private async Task AdminLogin()
    {
        LoginErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await _api.LoginAsync(AdminUsernameOrEmail, AdminPassword);
            if (!result.IsSuccess)
            {
                LoginErrorMessage = result.Error;
                return;
            }

            _sessionToken = result.Value!.SessionToken;
            IsLoggedIn = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsLoggedInChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task Load()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var kingdomsResult = await _api.GetKingdomsAsync();
            Kingdoms.Clear();
            if (kingdomsResult.IsSuccess)
            {
                foreach (var kingdom in kingdomsResult.Value!.OrderBy(k => k.Id))
                {
                    Kingdoms.Add(kingdom);
                }
            }

            var dungeonsResult = await _api.GetDungeonsAsync();
            Dungeons.Clear();
            if (dungeonsResult.IsSuccess)
            {
                foreach (var dungeon in dungeonsResult.Value!.OrderBy(d => d.Id))
                {
                    Dungeons.Add(dungeon);
                }
            }
            else
            {
                StatusMessage = dungeonsResult.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedDungeonChanged(DungeonData? value)
    {
        if (value is null)
        {
            return;
        }

        EditingId = value.Id;
        Name = value.Name;
        Description = value.Description;
        Seed = value.Seed;
        SelectedKingdom = Kingdoms.FirstOrDefault(k => k.Id == value.KingdomId);
        PreviewRooms.Clear();
        RoomVisuals.Clear();
        DoorVisuals.Clear();
    }

    [RelayCommand]
    private void NewDungeon()
    {
        SelectedDungeon = null;
        EditingId = null;
        Name = string.Empty;
        Description = string.Empty;
        Seed = 1000;
        SelectedKingdom = Kingdoms.FirstOrDefault();
        PreviewRooms.Clear();
        RoomVisuals.Clear();
        DoorVisuals.Clear();
        StatusMessage = null;
    }

    private bool CanSave() => IsLoggedIn;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (SelectedKingdom is null)
        {
            StatusMessage = "Choisissez un royaume.";
            return;
        }

        if (_sessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var dungeon = new DungeonData
            {
                Id = EditingId ?? 0,
                Name = Name,
                KingdomId = SelectedKingdom.Id,
                Description = Description,
                Seed = Seed,
            };

            var result = EditingId is { } id
                ? await _api.UpdateAsync(id, dungeon, _sessionToken)
                : await _api.CreateAsync(dungeon, _sessionToken);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            var savedIndex = Dungeons.ToList().FindIndex(d => d.Id == result.Value!.Id);
            if (savedIndex >= 0)
            {
                Dungeons[savedIndex] = result.Value!;
            }
            else
            {
                Dungeons.Add(result.Value!);
            }

            EditingId = result.Value!.Id;
            StatusMessage = "Enregistré.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDelete() => IsLoggedIn;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        if (EditingId is not { } id || _sessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.DeleteAsync(id, _sessionToken);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            var existing = Dungeons.FirstOrDefault(d => d.Id == id);
            if (existing is not null)
            {
                Dungeons.Remove(existing);
            }

            NewDungeon();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewFloor()
    {
        if (EditingId is not { } id)
        {
            StatusMessage = "Sélectionnez d'abord un donjon enregistré.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.GetFloorAsync(id, PreviewFloorNumber);
            PreviewRooms.Clear();
            RoomVisuals.Clear();
            DoorVisuals.Clear();

            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            var rooms = result.Value!.Rooms;
            foreach (var room in rooms)
            {
                PreviewRooms.Add($"Salle {room.Index} — {room.EncounterType}");
            }

            // Voir Docs/Idees.md — rendu visuel de grille : positionne chaque salle sur un
            // Canvas à partir de GridX/GridY (recalés pour rester en coordonnées positives, la
            // marche aléatoire du générateur peut s'étendre dans n'importe quelle direction
            // depuis la salle de départ) et trace un connecteur par porte Est/Sud (une porte
            // Nord/Ouest est nécessairement la porte Sud/Est symétrique d'une salle voisine déjà
            // parcourue, voir DungeonFloorGenerator — évite de dessiner chaque connecteur deux fois).
            if (rooms.Count > 0)
            {
                const double cellSize = 46;
                const double roomSize = 32;
                var minGridX = rooms.Min(r => r.GridX);
                var minGridY = rooms.Min(r => r.GridY);

                foreach (var room in rooms)
                {
                    var x = (room.GridX - minGridX) * cellSize;
                    var y = (room.GridY - minGridY) * cellSize;
                    RoomVisuals.Add(new RoomVisual(room.Index, room.EncounterType.ToString(), x, y, roomSize, ColorForEncounter(room.EncounterType)));

                    var centerX = x + roomSize / 2;
                    var centerY = y + roomSize / 2;
                    if (room.East)
                    {
                        DoorVisuals.Add(new DoorLineVisual(centerX, centerY, centerX + cellSize, centerY));
                    }

                    if (room.South)
                    {
                        DoorVisuals.Add(new DoorLineVisual(centerX, centerY, centerX, centerY + cellSize));
                    }
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Voir Docs/Idees.md — même palette que <c>Client/Program.cs DungeonRoomColor</c> (couleur des points sur la mini-carte du couloir), pour rester visuellement cohérent entre les deux outils.</summary>
    private static Brush ColorForEncounter(DungeonEncounterType type) => type switch
    {
        DungeonEncounterType.Monstre => new SolidColorBrush(Color.FromRgb(163, 74, 74)),
        DungeonEncounterType.MiniBoss => new SolidColorBrush(Color.FromRgb(191, 115, 46)),
        DungeonEncounterType.Boss => new SolidColorBrush(Color.FromRgb(179, 51, 51)),
        DungeonEncounterType.BossLegendaire => new SolidColorBrush(Color.FromRgb(217, 130, 26)),
        DungeonEncounterType.Coffre => new SolidColorBrush(Color.FromRgb(191, 158, 51)),
        DungeonEncounterType.SalleSecrete => new SolidColorBrush(Color.FromRgb(115, 89, 179)),
        DungeonEncounterType.Piege => new SolidColorBrush(Color.FromRgb(140, 51, 130)),
        DungeonEncounterType.Enigme => new SolidColorBrush(Color.FromRgb(51, 130, 140)),
        DungeonEncounterType.Marchand => new SolidColorBrush(Color.FromRgb(89, 140, 89)),
        DungeonEncounterType.Autel => new SolidColorBrush(Color.FromRgb(179, 179, 89)),
        DungeonEncounterType.Evenement => new SolidColorBrush(Color.FromRgb(89, 115, 179)),
        _ => new SolidColorBrush(Color.FromRgb(90, 90, 102)),
    };
}
