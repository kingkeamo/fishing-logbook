namespace FishingLogBook.Mobile.Common.Offline.Sqlite;

public interface ILocalDatabaseInitializedObserver
{
    Task OnDatabaseInitializedAsync(CancellationToken cancellationToken);
}
