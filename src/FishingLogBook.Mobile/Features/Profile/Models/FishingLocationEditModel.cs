using System.ComponentModel;

namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed class FishingLocationEditModel(Guid id, string name, bool isDefault) : INotifyPropertyChanged
{
    private string _name = name;
    private bool _isDefault = isDefault;

    public Guid Id { get; } = id;
    public Guid SelectionId { get; } = id == Guid.Empty ? Guid.NewGuid() : id;
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
    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (_isDefault == value)
            {
                return;
            }

            _isDefault = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDefault)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
