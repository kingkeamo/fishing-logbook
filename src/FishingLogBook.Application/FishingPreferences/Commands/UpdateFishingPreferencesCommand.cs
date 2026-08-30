using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.FishingPreferences.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.FishingPreferences.Commands;

public sealed class UpdateFishingPreferencesCommand : IRequest<UpdateFishingPreferencesResponse>
{
    public Guid UserId { get; init; }

    public UpdateFishingPreferencesDto Preferences { get; init; } = new([]);
}

public sealed class UpdateFishingPreferencesResponse : ValidatedResponse
{
    public FishingPreferencesDto? Preferences { get; init; }
}

public sealed class UpdateFishingPreferencesHandler
    : IRequestHandler<UpdateFishingPreferencesCommand, UpdateFishingPreferencesResponse>
{
    private readonly IFishingPreferenceService _fishingPreferenceService;

    public UpdateFishingPreferencesHandler(IFishingPreferenceService fishingPreferenceService)
    {
        _fishingPreferenceService = fishingPreferenceService;
    }

    public async Task<UpdateFishingPreferencesResponse> Handle(
        UpdateFishingPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _fishingPreferenceService.UpdatePreferencesAsync(
            command.UserId,
            command.Preferences,
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<UpdateFishingPreferencesResponse>(result.Errors[0]);
        }

        return new UpdateFishingPreferencesResponse
        {
            Preferences = result.Value
        };
    }
}

public sealed class UpdateFishingPreferencesCommandValidator : AbstractValidator<UpdateFishingPreferencesCommand>
{
    public UpdateFishingPreferencesCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
        RuleFor(command => command.Preferences)
            .NotNull();
        RuleFor(command => command.Preferences.Methods)
            .NotNull()
            .When(command => command.Preferences is not null);
        RuleFor(command => command.Preferences.Methods)
            .Must(HaveDistinctMethods)
            .WithMessage("A fishing method can only be selected once.")
            .Must(HaveAtMostOneDefaultMethod)
            .WithMessage("Only one fishing method can be the default.")
            .When(command => command.Preferences?.Methods is not null);
        RuleForEach(command => command.Preferences.Methods)
            .ChildRules(method =>
            {
                method.RuleFor(value => value.FishingMethodId)
                    .NotEmpty();
                method.RuleFor(value => value.Species)
                    .NotNull();
                method.RuleFor(value => value.Species)
                    .Must(HaveDistinctSpecies)
                    .WithMessage("A species can only be selected once for a fishing method.")
                    .Must(HaveAtMostOneDefaultSpecies)
                    .WithMessage("Only one species can be the default for a fishing method.")
                    .When(value => value.Species is not null);
                method.RuleForEach(value => value.Species)
                    .ChildRules(species => species.RuleFor(value => value.SpeciesId).NotEmpty())
                    .When(value => value.Species is not null);
            })
            .When(command => command.Preferences?.Methods is not null);
    }

    private static bool HaveDistinctMethods(IReadOnlyList<UpdateFishingMethodPreferenceDto> methods)
    {
        return methods.Select(method => method.FishingMethodId).Distinct().Count() == methods.Count;
    }

    private static bool HaveAtMostOneDefaultMethod(IReadOnlyList<UpdateFishingMethodPreferenceDto> methods)
    {
        return methods.Count(method => method.IsDefault) <= 1;
    }

    private static bool HaveDistinctSpecies(IReadOnlyList<UpdateFishingSpeciesPreferenceDto> species)
    {
        return species.Select(value => value.SpeciesId).Distinct().Count() == species.Count;
    }

    private static bool HaveAtMostOneDefaultSpecies(IReadOnlyList<UpdateFishingSpeciesPreferenceDto> species)
    {
        return species.Count(value => value.IsDefault) <= 1;
    }
}
