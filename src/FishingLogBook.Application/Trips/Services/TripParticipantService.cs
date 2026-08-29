using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripParticipantService : ITripParticipantService
{
    private readonly ITripAccessService _tripAccessService;
    private readonly ITripParticipantRepository _tripParticipantRepository;
    private readonly ITripRepository _tripRepository;
    private readonly IProfileRepository _profileRepository;
    private readonly IAnglerLookupService _anglerLookupService;
    private readonly ICurrentUser _currentUser;

    public TripParticipantService(
        ITripAccessService tripAccessService,
        ITripParticipantRepository tripParticipantRepository,
        ITripRepository tripRepository,
        IProfileRepository profileRepository,
        IAnglerLookupService anglerLookupService,
        ICurrentUser currentUser)
    {
        _tripAccessService = tripAccessService;
        _tripParticipantRepository = tripParticipantRepository;
        _tripRepository = tripRepository;
        _profileRepository = profileRepository;
        _anglerLookupService = anglerLookupService;
        _currentUser = currentUser;
    }

    public async Task<Result<TripParticipantsDto>> GetAsync(
        GetTripParticipantsArgs args,
        CancellationToken cancellationToken)
    {
        var access = await _tripAccessService.RequireContributorAsync(args.TripId, cancellationToken);
        return access.IsFailed
            ? Result.Fail<TripParticipantsDto>(access.Errors)
            : await BuildAsync(access.Value, cancellationToken);
    }

    public async Task<Result<TripParticipantsDto>> InviteAsync(
        InviteTripParticipantArgs args,
        CancellationToken cancellationToken)
    {
        var access = await _tripAccessService.RequireOwnerAsync(args.TripId, cancellationToken);
        if (access.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(access.Errors);
        }

        if (args.InvitedUserId == access.Value.Trip.OwnerUserId)
        {
            return Result.Fail<TripParticipantsDto>(new TripParticipantSelfInviteError());
        }

        var exists = await _profileRepository.UserExistsAsync(args.InvitedUserId, cancellationToken);
        if (exists.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(exists.Errors);
        }

        if (!exists.Value)
        {
            return Result.Fail<TripParticipantsDto>(new TripParticipantUserNotFoundError());
        }

        var existing = await FindAsync(args.TripId, args.InvitedUserId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(existing.Errors);
        }

        var invitation = ToInvitation(existing.Value, args, access.Value.Trip.OwnerUserId);
        if (invitation.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(invitation.Errors);
        }

        var saved = await _tripParticipantRepository.UpsertAsync(invitation.Value, cancellationToken);
        if (saved.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(saved.Errors);
        }

        return await BuildAsync(access.Value, cancellationToken);
    }

    public async Task<Result<TripParticipantsDto>> RemoveAsync(
        RemoveTripParticipantArgs args,
        CancellationToken cancellationToken)
    {
        var access = await _tripAccessService.RequireOwnerAsync(args.TripId, cancellationToken);
        if (access.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(access.Errors);
        }

        var existing = await FindAsync(args.TripId, args.ParticipantUserId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(existing.Errors);
        }

        if (existing.Value is null || existing.Value.RemovedOn is not null)
        {
            return Result.Fail<TripParticipantsDto>(new TripParticipantNotFoundError());
        }

        var removed = await _tripParticipantRepository.UpsertAsync(
            existing.Value.Status == TripParticipantStatusEnum.Accepted
                ? existing.Value.RemovedAt(DateTimeOffset.UtcNow)
                : existing.Value.RespondedAt(TripParticipantStatusEnum.Declined, DateTimeOffset.UtcNow),
            cancellationToken);
        if (removed.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(removed.Errors);
        }

        return await BuildAsync(access.Value, cancellationToken);
    }

    public async Task<Result> RespondAsync(
        RespondToTripInvitationArgs args,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail(new CurrentUserUnresolvedError());
        }

        if (args.Response is not (TripParticipantStatusEnum.Accepted or TripParticipantStatusEnum.Declined))
        {
            return Result.Fail(new TripInvitationNotFoundError());
        }

        var existing = await FindAsync(args.TripId, _currentUser.UserId, cancellationToken);
        if (existing.IsFailed)
        {
            return existing.ToResult();
        }

        if (existing.Value is null || existing.Value.RemovedOn is not null)
        {
            return Result.Fail(new TripInvitationNotFoundError());
        }

        if (!existing.Value.IsPending)
        {
            return Result.Fail(new TripParticipantAlreadyRespondedError());
        }

        var responded = await _tripParticipantRepository.UpsertAsync(
            existing.Value.RespondedAt(args.Response, DateTimeOffset.UtcNow),
            cancellationToken);
        return responded.ToResult();
    }

    public async Task<Result<IReadOnlyList<TripInvitationDto>>> GetMyInvitationsAsync(
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<IReadOnlyList<TripInvitationDto>>(new CurrentUserUnresolvedError());
        }

        var pending = await _tripParticipantRepository.GetPendingInvitationsByUserIdAsync(
            _currentUser.UserId,
            cancellationToken);
        if (pending.IsFailed)
        {
            return Result.Fail<IReadOnlyList<TripInvitationDto>>(pending.Errors);
        }

        var invitations = new List<TripInvitationDto>(pending.Value.Count);
        foreach (var participant in pending.Value)
        {
            var invitation = await ToInvitationDtoAsync(participant, cancellationToken);
            if (invitation.IsFailed)
            {
                return Result.Fail<IReadOnlyList<TripInvitationDto>>(invitation.Errors);
            }

            if (invitation.Value is not null)
            {
                invitations.Add(invitation.Value);
            }
        }

        return Result.Ok<IReadOnlyList<TripInvitationDto>>(invitations);
    }

    private async Task<Result<TripInvitationDto?>> ToInvitationDtoAsync(
        TripParticipant participant,
        CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdAsync(participant.TripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<TripInvitationDto?>(trip.Errors);
        }

        if (trip.Value is null)
        {
            return Result.Ok<TripInvitationDto?>(null);
        }

        var described = await _anglerLookupService.DescribeAsync([trip.Value.OwnerUserId], cancellationToken);
        if (described.IsFailed)
        {
            return Result.Fail<TripInvitationDto?>(described.Errors);
        }

        return Result.Ok<TripInvitationDto?>(new TripInvitationDto(
            trip.Value.Id,
            trip.Value.OwnerUserId,
            described.Value.GetValueOrDefault(trip.Value.OwnerUserId)?.DisplayName,
            trip.Value.Title,
            trip.Value.PlaceName,
            trip.Value.StartedOn,
            participant.InvitedOn));
    }

    private static Result<TripParticipant> ToInvitation(
        TripParticipant? existing,
        InviteTripParticipantArgs args,
        Guid ownerUserId)
    {
        if (existing is null)
        {
            return Result.Ok(new TripParticipant
            {
                Id = Guid.NewGuid(),
                TripId = args.TripId,
                UserId = args.InvitedUserId,
                Status = TripParticipantStatusEnum.Pending,
                InvitedByUserId = ownerUserId,
                InvitedOn = DateTimeOffset.UtcNow
            });
        }

        if (existing.IsPending || existing.IsContributing)
        {
            return Result.Fail<TripParticipant>(new TripParticipantAlreadyInvitedError());
        }

        return Result.Ok(existing.ReinvitedAt(ownerUserId, DateTimeOffset.UtcNow));
    }

    private async Task<Result<TripParticipant?>> FindAsync(
        Guid tripId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _tripParticipantRepository.FindAsync(
            new FindTripParticipantArgs { TripId = tripId, UserId = userId },
            cancellationToken);
    }

    private async Task<Result<TripParticipantsDto>> BuildAsync(
        TripAccess access,
        CancellationToken cancellationToken)
    {
        var trip = access.Trip;
        var participants = await _tripParticipantRepository.GetByTripIdAsync(trip.Id, cancellationToken);
        if (participants.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(participants.Errors);
        }

        var visible = participants.Value
            .Where(participant => participant.IsPending || participant.IsContributing)
            .OrderBy(participant => participant.InvitedOn)
            .ThenBy(participant => participant.UserId)
            .ToArray();
        var described = await _anglerLookupService.DescribeAsync(
            [.. visible.Select(participant => participant.UserId).Append(trip.OwnerUserId)],
            cancellationToken);
        if (described.IsFailed)
        {
            return Result.Fail<TripParticipantsDto>(described.Errors);
        }

        return Result.Ok(new TripParticipantsDto(trip.Id, ToRole(access.Role))
        {
            Participants =
            [
                Owner(trip.OwnerUserId, described.Value),
                .. visible.Select(participant => ToDto(participant, described.Value))
            ]
        });
    }

    private static TripParticipantDto Owner(
        Guid ownerUserId,
        IReadOnlyDictionary<Guid, AnglerSummaryDto> described)
    {
        var angler = described.GetValueOrDefault(ownerUserId);
        return new TripParticipantDto(
            ownerUserId,
            TripParticipantConstants.Accepted,
            angler?.DisplayName,
            angler?.PhotographUrl,
            DateTimeOffset.MinValue)
        {
            IsOwner = true
        };
    }

    private static TripParticipantDto ToDto(
        TripParticipant participant,
        IReadOnlyDictionary<Guid, AnglerSummaryDto> described)
    {
        var angler = described.GetValueOrDefault(participant.UserId);
        return new TripParticipantDto(
            participant.UserId,
            participant.Status.ToString(),
            angler?.DisplayName,
            angler?.PhotographUrl,
            participant.InvitedOn)
        {
            RespondedOn = participant.RespondedOn
        };
    }

    private static string ToRole(TripAccessRoleEnum role)
    {
        return role switch
        {
            TripAccessRoleEnum.Owner => TripParticipantConstants.Owner,
            TripAccessRoleEnum.Participant => TripParticipantConstants.Participant,
            _ => TripParticipantConstants.None
        };
    }
}
