using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Contracts.Services;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Domain.Enums;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Capabilities.Commands;

public sealed class GrantPlatformCapabilityCommand : IRequest<GrantPlatformCapabilityResponse>
{
    public Guid TargetUserId { get; init; }

    public PlatformCapabilityEnum Capability { get; init; }
}

public sealed class GrantPlatformCapabilityResponse : ValidatedResponse
{
}

public sealed class GrantPlatformCapabilityHandler : IRequestHandler<GrantPlatformCapabilityCommand, GrantPlatformCapabilityResponse>
{
    private readonly IPlatformCapabilityService _platformCapabilityService;
    private readonly IMapper _mapper;

    public GrantPlatformCapabilityHandler(IPlatformCapabilityService platformCapabilityService, IMapper mapper)
    {
        _platformCapabilityService = platformCapabilityService;
        _mapper = mapper;
    }

    public async Task<GrantPlatformCapabilityResponse> Handle(
        GrantPlatformCapabilityCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _platformCapabilityService.GrantAsync(
            _mapper.Map<GrantPlatformCapabilityArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GrantPlatformCapabilityResponse>(result.Errors[0]);
        }

        return new GrantPlatformCapabilityResponse();
    }
}

public sealed class GrantPlatformCapabilityCommandValidator : AbstractValidator<GrantPlatformCapabilityCommand>
{
    public GrantPlatformCapabilityCommandValidator()
    {
        RuleFor(command => command.TargetUserId)
            .NotEmpty();
        RuleFor(command => command.Capability)
            .IsInEnum();
    }
}
