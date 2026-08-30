using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class CreateTripPhotographUploadCommand : IRequest<CreateTripPhotographUploadResponse>
{
    public Guid TripId { get; init; }

    public PhotographUploadRequestDto Request { get; init; } = new(Guid.Empty, string.Empty);
}

public sealed class CreateTripPhotographUploadResponse : ValidatedResponse
{
    public PhotographUploadDto? Upload { get; init; }
}

public sealed class CreateTripPhotographUploadHandler
    : IRequestHandler<CreateTripPhotographUploadCommand, CreateTripPhotographUploadResponse>
{
    private readonly ITripPhotographService _tripPhotographService;
    private readonly IMapper _mapper;

    public CreateTripPhotographUploadHandler(ITripPhotographService tripPhotographService, IMapper mapper)
    {
        _tripPhotographService = tripPhotographService;
        _mapper = mapper;
    }

    public async Task<CreateTripPhotographUploadResponse> Handle(
        CreateTripPhotographUploadCommand command,
        CancellationToken cancellationToken)
    {
        if (!_tripPhotographService.IsObjectStorageConfigured)
        {
            return ValidatedResponse.FromError<CreateTripPhotographUploadResponse>(
                new TripObjectStorageNotConfiguredError());
        }

        var result = await _tripPhotographService.CreateUploadAsync(
            _mapper.Map<CreateTripPhotographUploadArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<CreateTripPhotographUploadResponse>(result.Errors[0]);
        }

        return new CreateTripPhotographUploadResponse
        {
            Upload = result.Value
        };
    }
}

public sealed class CreateTripPhotographUploadCommandValidator
    : AbstractValidator<CreateTripPhotographUploadCommand>
{
    public CreateTripPhotographUploadCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.Request.PhotographId)
            .NotEmpty();
        RuleFor(command => command.Request.ContentType)
            .Must(PhotographContentTypeConstants.IsAllowed)
            .WithMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }
}
