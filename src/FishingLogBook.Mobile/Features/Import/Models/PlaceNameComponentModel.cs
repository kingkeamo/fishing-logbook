using System.ComponentModel;

namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed class PlaceNameComponentModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public PlaceNameComponentModel(string text, bool isSelected)
    {
        Text = text;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text { get; }

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
}
