using System.Collections.ObjectModel;
using Aetheria.MapEditor.Services;
using Aetheria.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.MapEditor.ViewModels;

/// <summary>
/// Édition du catalogue de donjons (voir <c>Docs/GameDesign.md</c> — section Donjons) : liste
/// des donjons à gauche, formulaire d'édition à droite, et prévisualisation de la génération
/// procédurale d'un étage (texte uniquement — pas de rendu visuel de grille pour cette
/// première version, voir <c>Docs/README.md</c>).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DungeonApiClient _api = new();

    public ObservableCollection<DungeonData> Dungeons { get; } = [];
    public ObservableCollection<KingdomData> Kingdoms { get; } = [];
    public ObservableCollection<string> PreviewRooms { get; } = [];

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
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedKingdom is null)
        {
            StatusMessage = "Choisissez un royaume.";
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
                ? await _api.UpdateAsync(id, dungeon)
                : await _api.CreateAsync(dungeon);

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

    [RelayCommand]
    private async Task Delete()
    {
        if (EditingId is not { } id)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.DeleteAsync(id);
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

            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            foreach (var room in result.Value!.Rooms)
            {
                PreviewRooms.Add($"Salle {room.Index} — {room.EncounterType}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
