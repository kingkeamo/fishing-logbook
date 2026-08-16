using FluentValidation.Results;

namespace FishingLogBook.Application.Common.Responses;

public abstract class ValidatedResponse
{
    public bool IsSuccess =>
        string.IsNullOrWhiteSpace(ErrorMessage) &&
        (ValidationErrors is null || ValidationErrors.Count == 0);

    public bool IsFailure => !IsSuccess;

    public string? ErrorMessage { get; set; }

    public IList<ValidationFailure>? ValidationErrors { get; set; }
}
