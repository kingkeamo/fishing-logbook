using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Profiles.Commands;

public sealed class RecordProfilePhotographCommand : IRequest<RecordProfilePhotographResponse>
{
    public Guid UserId { get; init; }

    public RecordPhotographDto Photograph { get; init; } = new(Guid.Empty, string.Empty, string.Empty);
}

public sealed class RecordProfilePhotographResponse : ValidatedResponse
{
    public ProfileDto? Profile { get; init; }
}

public sealed class RecordProfilePhotographHandler
    : IRequestHandler<RecordProfilePhotographCommand, RecordProfilePhotographResponse>
{
    private readonly IProfileService _profileService;
    private readonly IMapper _mapper;

    public RecordProfilePhotographHandler(IProfileService profileService, IMapper mapper)
    {
        _profileService = profileService;
        _mapper = mapper;
    }

    public async Task<RecordProfilePhotographResponse> Handle(
        RecordProfilePhotographCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.RecordPhotographAsync(
            _mapper.Map<RecordProfilePhotographArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<RecordProfilePhotographResponse>(result.Errors[0]);
        }

        return new RecordProfilePhotographResponse
        {
            Profile = result.Value
        };
    }
}

public sealed class RecordProfilePhotographCommandValidator : AbstractValidator<RecordProfilePhotographCommand>
{
    public RecordProfilePhotographCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
        RuleFor(command => command.Photograph.PhotographId)
            .NotEmpty();
        RuleFor(command => command.Photograph.ObjectKey)
            .Must(value => !string.IsNullOrWhiteSpace(value));
        RuleFor(command => command.Photograph.ContentType)
            .Must(PhotographContentTypeConstants.IsAllowed)
            .WithMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }
}
