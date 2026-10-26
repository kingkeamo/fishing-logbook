using System.ComponentModel;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class CatchFilterDraftModel : INotifyPropertyChanged
{
    private CatchMethodEnum _method;
    private CatchDateRangeEnum _dateRange;
    private string _location;

    public CatchFilterDraftModel(UiTextService text, CatchFilterModel filter, IReadOnlyList<SpeciesOptionModel> species)
    {
        Text = text;
        _method = filter.Method;
        _dateRange = filter.DateRange;
        _location = filter.Location;
        Species = species;
        foreach (var option in species)
        {
            option.PropertyChanged += OnSpeciesChanged;
        }
    }

    public UiTextService Text { get; }
    public IReadOnlyList<SpeciesOptionModel> Species { get; }
    public CatchMethodEnum Method => _method;
    public CatchDateRangeEnum DateRange => _dateRange;
    public string Location => _location;
    public bool AllMethodsSelected => _method == CatchMethodEnum.All;
    public bool FlySelected => _method == CatchMethodEnum.Fly;
    public bool SpinningSelected => _method == CatchMethodEnum.Spinning;
    public bool LureSelected => _method == CatchMethodEnum.Lure;
    public bool OtherSelected => _method == CatchMethodEnum.Other;
    public bool AllTimeSelected => _dateRange == CatchDateRangeEnum.AllTime;
    public bool Last7DaysSelected => _dateRange == CatchDateRangeEnum.Last7Days;
    public bool Last30DaysSelected => _dateRange == CatchDateRangeEnum.Last30Days;
    public int ActiveCount => ToFilter().ActiveCount;
    public string ApplyText => ActiveCount == 0 ? Text["ApplyFilters"] : string.Format(Text.Culture, Text["ApplyFiltersCount"], ActiveCount);
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetMethod(CatchMethodEnum method)
    {
        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        _method = method;
        Notify(nameof(Method), nameof(AllMethodsSelected), nameof(FlySelected), nameof(SpinningSelected), nameof(LureSelected), nameof(OtherSelected));
    }

    public void SetDateRange(CatchDateRangeEnum dateRange)
    {
        if (!Enum.IsDefined(dateRange))
        {
            throw new ArgumentOutOfRangeException(nameof(dateRange));
        }

        _dateRange = dateRange;
        Notify(nameof(DateRange), nameof(AllTimeSelected), nameof(Last7DaysSelected), nameof(Last30DaysSelected));
    }

    public void SetLocation(string? location)
    {
        _location = location ?? string.Empty;
        Notify(nameof(Location));
    }

    public void Clear()
    {
        foreach (var option in Species)
        {
            option.IsSelected = false;
        }

        SetMethod(CatchMethodEnum.All);
        SetDateRange(CatchDateRangeEnum.AllTime);
        SetLocation(null);
    }

    public CatchFilterModel ToFilter()
    {
        var selected = Species.Where(option => option.IsSelected).Select(option => option.Species).ToHashSet();
        return new CatchFilterModel(_method, selected, _dateRange, _location.Trim());
    }

    private void OnSpeciesChanged(object? sender, PropertyChangedEventArgs args)
    {
        Notify();
    }

    private void Notify(params string[] changed)
    {
        foreach (var property in changed.Concat([nameof(ActiveCount), nameof(ApplyText)]))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
