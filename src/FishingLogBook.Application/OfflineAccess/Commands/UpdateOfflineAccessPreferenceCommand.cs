using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.OfflineAccess.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.OfflineAccess.Commands;

public sealed class UpdateOfflineAccessPreferenceCommand : IRequest<UpdateOfflineAccessPreferenceResponse>
{
    public Guid UserId { get; init; }
    public bool Enabled { get; init; }
}

public sealed class UpdateOfflineAccessPreferenceResponse : ValidatedResponse
{
    public OfflineAccessPreferenceDto? Preference { get; init; }
}

public sealed class UpdateOfflineAccessPreferenceHandler : IRequestHandler<UpdateOfflineAccessPreferenceCommand, UpdateOfflineAccessPreferenceResponse>
{
    private readonly IOfflineAccessPreferenceService _service;

    public UpdateOfflineAccessPreferenceHandler(IOfflineAccessPreferenceService service) => _service = service;

    public async Task<UpdateOfflineAccessPreferenceResponse> Handle(UpdateOfflineAccessPreferenceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SetAsync(command.UserId, command.Enabled, cancellationToken);
        return result.IsFailed
            ? new UpdateOfflineAccessPreferenceResponse { ErrorMessage = result.Errors[0].Message }
            : new UpdateOfflineAccessPreferenceResponse { Preference = result.Value };
    }
}

public sealed class UpdateOfflineAccessPreferenceCommandValidator : AbstractValidator<UpdateOfflineAccessPreferenceCommand>
{
    public UpdateOfflineAccessPreferenceCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}
