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

    /// <summary>Voir Docs/Idees.md — le type (rôle de combat) existait déjà sur MonsterSpeciesData, seul un ComboBox manquait ici (auparavant modifiable uniquement via l'API/le seeder).</summary>
    public IReadOnlyList<MonsterType> AvailableTypes { get; } = Enum.GetValues<MonsterType>();

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
    private MonsterType _type = MonsterType.Guerrier;

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
        Type = value.Type;
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
        Type = MonsterType.Guerrier;
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

    private bool CanSave() => IsLoggedIn;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (_sessionToken is null)
        {
            return;
        }

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
                Type = Type,
                Habitat = Habitat,
                Lore = Lore,
                BaseStats = new StatBlock(Health, Attack, Defense, Speed, Intelligence, Resistance),
                EvolvesIntoSpeciesId = EvolvesIntoSpeciesId,
                EvolutionLevel = EvolutionLevel,
            };

            var result = EditingId is { } id
                ? await _api.UpdateAsync(id, species, _sessionToken)
                : await _api.CreateAsync(species, _sessionToken);

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
