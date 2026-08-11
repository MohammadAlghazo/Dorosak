using FluentValidation;

namespace Dorosak.Application.Features.Communications;

internal sealed class GetConversationsQueryValidator : AbstractValidator<GetConversationsQuery>
{
    public GetConversationsQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Limit).InclusiveBetween(1, 50);
        RuleFor(request => request.Cursor).MaximumLength(1000);
    }
}

internal sealed class GetNotificationsQueryValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).MaximumLength(1000);
        RuleFor(request => request.AfterSequence).GreaterThanOrEqualTo(0).When(request => request.AfterSequence.HasValue);
    }
}

internal sealed class GetNotificationUnreadCountQueryValidator : AbstractValidator<GetNotificationUnreadCountQuery>
{
    public GetNotificationUnreadCountQueryValidator() => RuleFor(request => request.UserId).NotEmpty();
}

internal sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.NotificationId).NotEmpty();
    }
}

internal sealed class MarkAllNotificationsReadCommandValidator : AbstractValidator<MarkAllNotificationsReadCommand>
{
    public MarkAllNotificationsReadCommandValidator() => RuleFor(request => request.UserId).NotEmpty();
}

internal sealed class GetAnnouncementsQueryValidator : AbstractValidator<GetAnnouncementsQuery>
{
    public GetAnnouncementsQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).MaximumLength(1000);
    }
}

internal sealed class GetAnnouncementQueryValidator : AbstractValidator<GetAnnouncementQuery>
{
    public GetAnnouncementQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.AnnouncementId).NotEmpty();
    }
}

internal sealed class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Body).NotEmpty().MaximumLength(10000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateAnnouncementCommandValidator : AbstractValidator<UpdateAnnouncementCommand>
{
    public UpdateAnnouncementCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.AnnouncementId).NotEmpty();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Body).NotEmpty().MaximumLength(10000);
        RuleFor(request => request.ExpectedVersion).GreaterThanOrEqualTo(1);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class DeleteAnnouncementCommandValidator : AbstractValidator<DeleteAnnouncementCommand>
{
    public DeleteAnnouncementCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.AnnouncementId).NotEmpty();
        RuleFor(request => request.ExpectedVersion).GreaterThanOrEqualTo(1);
    }
}

internal sealed class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.ParticipantUserIds).NotNull().NotEmpty().Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("A conversation cannot contain more than 50 requested participants.");
        RuleForEach(request => request.ParticipantUserIds).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class GetConversationMessagesQueryValidator : AbstractValidator<GetConversationMessagesQuery>
{
    public GetConversationMessagesQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.ConversationId).NotEmpty();
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).MaximumLength(1000);
        RuleFor(request => request.AfterSequence).GreaterThanOrEqualTo(0).When(request => request.AfterSequence.HasValue);
    }
}

internal sealed class CreateMessageCommandValidator : AbstractValidator<CreateMessageCommand>
{
    public CreateMessageCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.ConversationId).NotEmpty();
        RuleFor(request => request.ClientMessageId).NotEmpty();
        RuleFor(request => request.Body).NotEmpty().MaximumLength(5000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class LeaveConversationCommandValidator : AbstractValidator<LeaveConversationCommand>
{
    public LeaveConversationCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.ConversationId).NotEmpty();
    }
}
