using System.ComponentModel;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class SpeciesOptionModel(CatchSpeciesEnum species, string label, int count, bool isSelected) : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;

    public CatchSpeciesEnum Species { get; } = species;
    public string Label { get; } = label;
    public int Count { get; } = count;
    public string CountText => Count.ToString();

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
