using System.Net;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Trips.Clients;

public sealed class TripParticipantClient : ITripParticipantClient
{
    private readonly HttpClient _apiClient;

    public TripParticipantClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
    }

    public async Task<TripParticipantsDto?> GetAsync(Guid tripId, CancellationToken cancellationToken)
    {
        using var response = await _apiClient.GetAsync(
            $"api/trips/{tripId:D}/participants",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TripParticipantsDto>(cancellationToken);
    }

    public async Task<TripParticipantsDto?> InviteAsync(
        Guid tripId,
        InviteTripParticipantDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/trips/{tripId:D}/participants",
            request,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TripParticipantsDto>(cancellationToken);
    }

    public async Task<TripParticipantsDto?> RemoveAsync(
        Guid tripId,
        Guid participantUserId,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.DeleteAsync(
            $"api/trips/{tripId:D}/participants/{participantUserId:D}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TripParticipantsDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<TripInvitationDto>> GetMyInvitationsAsync(
        CancellationToken cancellationToken)
    {
        var invitations = await _apiClient.GetFromJsonAsync<IReadOnlyList<TripInvitationDto>>(
            "api/trips/invitations",
            cancellationToken);
        return invitations ?? [];
    }

    public async Task<bool> AcceptAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return await RespondAsync(tripId, "accept", cancellationToken);
    }

    public async Task<bool> DeclineAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return await RespondAsync(tripId, "decline", cancellationToken);
    }

    private async Task<bool> RespondAsync(
        Guid tripId,
        string response,
        CancellationToken cancellationToken)
    {
        using var result = await _apiClient.PostAsync(
            $"api/trips/{tripId:D}/invitation/{response}",
            content: null,
            cancellationToken);
        return result.IsSuccessStatusCode;
    }
}
