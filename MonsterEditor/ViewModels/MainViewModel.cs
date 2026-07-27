using System.Collections.ObjectModel;
using Aetheria.MonsterEditor.Services;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.MonsterEditor.ViewModels;

/// <summary>
/// Édition du bestiaire (voir <c>Docs/GameDesign.md</c> — section Bestiaire) : liste des
/// espèces à gauche, formulaire d'édition à droite, CRUD via l'API du Server.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly MonsterSpeciesApiClient _api = new();

    public ObservableCollection<MonsterSpeciesData> Species { get; } = [];

    public IReadOnlyList<Aetheria.Shared.Enums.Element> AvailableElements { get; } = Enum.GetValues<Aetheria.Shared.Enums.Element>();
    public IReadOnlyList<Rarity> AvailableRarities { get; } = Enum.GetValues<Rarity>();

    [ObservableProperty]
    private MonsterSpeciesData? _selectedSpecies;

    [ObservableProperty]
    private int? _editingId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Element _element = Aetheria.Shared.Enums.Element.Neutre;

    [ObservableProperty]
    private Rarity _baseRarity = Rarity.Commun;

    [ObservableProperty]
    private string _habitat = string.Empty;

    [ObservableProperty]
    private string _lore = string.Empty;

    [ObservableProperty]
    private int _health = 20;

    [ObservableProperty]
    private int _attack = 10;

    [ObservableProperty]
    private int _defense = 10;

    [ObservableProperty]
    private int _speed = 10;

    [ObservableProperty]
    private int _intelligence = 10;

    [ObservableProperty]
    private int _resistance = 10;

    [ObservableProperty]
    private int? _evolvesIntoSpeciesId;

    [ObservableProperty]
    private int _evolutionLevel;

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
            var result = await _api.GetAllAsync();
            Species.Clear();
            if (result.IsSuccess)
            {
                foreach (var species in result.Value!.OrderBy(s => s.Id))
                {
                    Species.Add(species);
                }
            }
            else
            {
                StatusMessage = result.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSpeciesChanged(MonsterSpeciesData? value)
    {
        if (value is null)
        {
            return;
        }

        EditingId = value.Id;
        Name = value.Name;
        Element = value.Element;
        BaseRarity = value.BaseRarity;
        Habitat = value.Habitat;
        Lore = value.Lore;
        Health = value.BaseStats.Health;
        Attack = value.BaseStats.Attack;
        Defense = value.BaseStats.Defense;
        Speed = value.BaseStats.Speed;
        Intelligence = value.BaseStats.Intelligence;
        Resistance = value.BaseStats.Resistance;
        EvolvesIntoSpeciesId = value.EvolvesIntoSpeciesId;
        EvolutionLevel = value.EvolutionLevel;
    }

    [RelayCommand]
    private void NewSpecies()
    {
        SelectedSpecies = null;
        EditingId = null;
        Name = string.Empty;
        Element = Aetheria.Shared.Enums.Element.Neutre;
        BaseRarity = Rarity.Commun;
        Habitat = string.Empty;
        Lore = string.Empty;
        Health = 20;
        Attack = 10;
        Defense = 10;
        Speed = 10;
        Intelligence = 10;
        Resistance = 10;
        EvolvesIntoSpeciesId = null;
        EvolutionLevel = 0;
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task Save()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var species = new MonsterSpeciesData
            {
                Id = EditingId ?? 0,
                Name = Name,
                Element = Element,
                BaseRarity = BaseRarity,
                Habitat = Habitat,
                Lore = Lore,
                BaseStats = new StatBlock(Health, Attack, Defense, Speed, Intelligence, Resistance),
                EvolvesIntoSpeciesId = EvolvesIntoSpeciesId,
                EvolutionLevel = EvolutionLevel,
            };

            var result = EditingId is { } id
                ? await _api.UpdateAsync(id, species)
                : await _api.CreateAsync(species);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            var savedIndex = Species.ToList().FindIndex(s => s.Id == result.Value!.Id);
            if (savedIndex >= 0)
            {
                Species[savedIndex] = result.Value!;
            }
            else
            {
                Species.Add(result.Value!);
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

            var existing = Species.FirstOrDefault(s => s.Id == id);
            if (existing is not null)
            {
                Species.Remove(existing);
            }

            NewSpecies();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
