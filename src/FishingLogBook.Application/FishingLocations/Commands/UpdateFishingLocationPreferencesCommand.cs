using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.FishingLocations.Commands;

public sealed class UpdateFishingLocationPreferencesCommand
    : IRequest<UpdateFishingLocationPreferencesResponse>
{
    public Guid UserId { get; init; }

    public UpdateFishingLocationPreferencesDto Locations { get; init; } = new([]);
}

public sealed class UpdateFishingLocationPreferencesResponse : ValidatedResponse
{
    public FishingLocationPreferencesDto? Locations { get; init; }
}

public sealed class UpdateFishingLocationPreferencesHandler
    : IRequestHandler<UpdateFishingLocationPreferencesCommand, UpdateFishingLocationPreferencesResponse>
{
    private readonly IFishingLocationPreferenceService _fishingLocationPreferenceService;

    public UpdateFishingLocationPreferencesHandler(
        IFishingLocationPreferenceService fishingLocationPreferenceService)
    {
        _fishingLocationPreferenceService = fishingLocationPreferenceService;
    }

    public async Task<UpdateFishingLocationPreferencesResponse> Handle(
        UpdateFishingLocationPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _fishingLocationPreferenceService.UpdatePreferencesAsync(
            command.UserId,
            command.Locations,
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<UpdateFishingLocationPreferencesResponse>(result.Errors[0]);
        }

        return new UpdateFishingLocationPreferencesResponse
        {
            Locations = result.Value
        };
    }
}

public sealed class UpdateFishingLocationPreferencesCommandValidator
    : AbstractValidator<UpdateFishingLocationPreferencesCommand>
{
    public UpdateFishingLocationPreferencesCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
        RuleFor(command => command.Locations)
            .NotNull();
        RuleFor(command => command.Locations.Locations)
            .NotNull()
            .When(command => command.Locations is not null);
        RuleFor(command => command.Locations.Locations)
            .Must(HaveDistinctNames)
            .WithMessage("A fishing location can only be saved once.")
            .Must(HaveAtMostOneDefault)
            .WithMessage("Only one fishing location can be the default.")
            .When(command => command.Locations?.Locations is not null);
        RuleForEach(command => command.Locations.Locations)
            .ChildRules(location =>
            {
                location.RuleFor(value => value.Name)
                    .Must(FishingLocationConstants.IsNameValid)
                    .WithMessage($"A fishing location name is required and must be {FishingLocationConstants.MaxNameLength} characters or fewer.");
            })
            .When(command => command.Locations?.Locations is not null);
    }

    private static bool HaveDistinctNames(IReadOnlyList<UpdateFishingLocationPreferenceDto> locations)
    {
        var names = locations
            .Select(location => FishingLocationConstants.TrimName(location.Name))
            .Where(name => name is not null)
            .Select(name => name!.ToLowerInvariant())
            .ToArray();
        return names.Distinct().Count() == names.Length;
    }

    private static bool HaveAtMostOneDefault(IReadOnlyList<UpdateFishingLocationPreferenceDto> locations)
    {
        return locations.Count(location => location.IsDefault) <= 1;
    }
}
