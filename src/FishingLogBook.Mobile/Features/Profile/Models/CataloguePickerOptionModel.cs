using System.ComponentModel;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed class CataloguePickerOptionModel(Guid id, string name, string removeDescription, bool isSelected)
    : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;

    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string RemoveDescription { get; } = removeDescription;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
