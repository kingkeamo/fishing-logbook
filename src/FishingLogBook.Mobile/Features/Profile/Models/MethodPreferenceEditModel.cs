using System.ComponentModel;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed class MethodPreferenceEditModel(
    Guid id,
    string code,
    string name,
    IReadOnlyList<SpeciesPreferenceEditModel> species) : INotifyPropertyChanged
{
    private string _name = name;
    private bool _isSelected;
    private bool _isDefault;
    private string _defaultDescription = string.Empty;
    private PreferenceMoreActionModel? _moreAction;

    public Guid Id { get; } = id;
    public string Code { get; } = code;
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }
    public IReadOnlyList<SpeciesPreferenceEditModel> Species { get; } = species;
    public IReadOnlyList<SpeciesPreferenceEditModel> SelectedSpecies => Species.Where(item => item.IsSelected).ToArray();
    public IReadOnlyList<object> SpeciesChipItems => [.. SelectedSpecies, MoreAction];
    public PreferenceMoreActionModel MoreAction => _moreAction ??= new PreferenceMoreActionModel(this);
    public string DefaultDescription
    {
        get => _defaultDescription;
        set
        {
            if (_defaultDescription == value)
            {
                return;
            }

            _defaultDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DefaultDescription)));
        }
    }
    public bool IsEnabled { get; set; } = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value, nameof(IsSelected));
    }
    public bool IsDefault
    {
        get => _isDefault;
        set => Set(ref _isDefault, value, nameof(IsDefault));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifySelectedSpecies()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSpecies)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpeciesChipItems)));
    }

    private void Set(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
