using System.ComponentModel;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class CatchPhotoThumbnailModel(int index, string path, bool isSelected, Func<int, string> describe) : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;

    public int Index { get; } = index;
    public string Path { get; } = path;
    public string Description => describe(Index);

    public bool IsSelected
    {
        get => _isSelected;
        internal set
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

    internal void RefreshDescription()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
    }
}
