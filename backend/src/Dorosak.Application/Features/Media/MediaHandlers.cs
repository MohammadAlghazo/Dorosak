using Dorosak.Application.Common.Results;
using FluentValidation;
using MediatR;

namespace Dorosak.Application.Features.Media;

internal sealed class MediaCommandHandler<TRequest, TResponse>(IMediaService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) =>
        request switch
        {
            CreateUploadSessionCommand command => Cast(service.CreateUploadSessionAsync(command, cancellationToken)),
            CreateCaptionUploadCommand command => Cast(service.CreateCaptionUploadAsync(command, cancellationToken)),
            PutUploadContentCommand command => Cast(service.PutUploadContentAsync(command, cancellationToken)),
            IssueUploadPartCommand command => Cast(service.IssueUploadPartAsync(command, cancellationToken)),
            CompleteUploadCommand command => Cast(service.CompleteUploadAsync(command, cancellationToken)),
            CancelUploadCommand command => Cast(service.CancelUploadAsync(command, cancellationToken)),
            CreateDownloadGrantCommand command => Cast(service.CreateDownloadGrantAsync(command, cancellationToken)),
            _ => throw new InvalidOperationException($"Unsupported media command {typeof(TRequest).Name}.")
        };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}

internal sealed class CreateCaptionUploadValidator : AbstractValidator<CreateCaptionUploadCommand>
{
    public CreateCaptionUploadValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.AssetId).NotEmpty();
        RuleFor(command => command.Locale).Matches("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$").MaximumLength(16);
        RuleFor(command => command.Label).NotEmpty().MaximumLength(120);
        RuleFor(command => command.ExpectedBytes).GreaterThan(0).LessThanOrEqualTo(10L * 1024 * 1024);
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255).Must(name => Path.GetExtension(name).Equals(".vtt", StringComparison.OrdinalIgnoreCase));
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class MediaQueryHandler<TRequest, TResponse>(IMediaService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) =>
        request switch
        {
            GetMediaStatusQuery query => Cast(service.GetStatusAsync(query, cancellationToken)),
            _ => throw new InvalidOperationException($"Unsupported media query {typeof(TRequest).Name}.")
        };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}

internal sealed class CreateUploadSessionValidator : AbstractValidator<CreateUploadSessionCommand>
{
    public CreateUploadSessionValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Purpose).NotEmpty().MaximumLength(40);
        RuleFor(command => command.ExpectedBytes).GreaterThan(0);
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255);
        RuleFor(command => command.ContentType).NotEmpty().MaximumLength(120);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ClientFileId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .When(command => command.Purpose.Equals(nameof(Dorosak.Domain.Media.MediaPurpose.AssignmentSubmission), StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class IssueUploadPartValidator : AbstractValidator<IssueUploadPartCommand>
{
    public IssueUploadPartValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.UploadSessionId).NotEmpty();
        RuleFor(command => command.PartNumber).InclusiveBetween(1, 10000);
        RuleFor(command => command.ExpectedBytes).GreaterThan(0);
        RuleFor(command => command.Sha256).Matches("^[0-9a-fA-F]{64}$");
    }
}

internal sealed class CompleteUploadValidator : AbstractValidator<CompleteUploadCommand>
{
    public CompleteUploadValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.UploadSessionId).NotEmpty();
        RuleFor(command => command.TotalBytes).GreaterThan(0);
        RuleFor(command => command.Sha256).Matches("^[0-9a-fA-F]{64}$");
        RuleFor(command => command.Parts).NotEmpty().Must(parts => parts.Select(part => part.PartNumber).Distinct().Count() == parts.Count)
            .WithMessage("Part numbers must be unique.");
        RuleForEach(command => command.Parts).ChildRules(part =>
        {
            part.RuleFor(item => item.PartNumber).InclusiveBetween(1, 10000);
            part.RuleFor(item => item.Size).GreaterThan(0);
            part.RuleFor(item => item.Sha256).Matches("^[0-9a-fA-F]{64}$");
            part.RuleFor(item => item.ETag).NotEmpty().MaximumLength(512);
        });
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CancelUploadValidator : AbstractValidator<CancelUploadCommand>
{
    public CancelUploadValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.UploadSessionId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
