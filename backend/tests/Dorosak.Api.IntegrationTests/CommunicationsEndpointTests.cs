using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Communications;
using Dorosak.Application.Features.Identity;
using Dorosak.Domain.Catalog;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CommunicationsEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task ConversationsRequireHeadersCurrentParticipantsAndNoStoreResponses()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpResponseMessage anonymous = await fixture.Client.GetAsync(
            "/api/v1/conversations",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        SignedInUser creator = await CreateUserAsync("communications-http-creator", cancellationToken);
        SignedInUser participant = await CreateUserAsync("communications-http-participant", cancellationToken);
        SignedInUser outsider = await CreateUserAsync("communications-http-outsider", cancellationToken);
        Guid courseId;
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            Course course = Course.Create(creator.UserId, "en", DateTimeOffset.UtcNow);
            dbContext.Set<Course>().Add(course);
            dbContext.Set<CourseInstructor>().Add(CourseInstructor.Create(
                course.Id,
                participant.UserId,
                CourseCollaboratorRole.Reviewer,
                DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync(cancellationToken);
            courseId = course.Id;
        }

        using HttpRequestMessage missingConversationKey = Authorized(
            HttpMethod.Post,
            "/api/v1/conversations",
            creator.AccessToken);
        missingConversationKey.Content = JsonContent.Create(new
        {
            participantUserIds = new[] { participant.UserId },
            courseId,
        });
        using HttpResponseMessage conversationPrecondition = await fixture.Client.SendAsync(
            missingConversationKey,
            cancellationToken);
        Assert.Equal((HttpStatusCode)428, conversationPrecondition.StatusCode);
        Assert.Equal("IDEMPOTENCY.KEY_REQUIRED", await ReadProblemCodeAsync(conversationPrecondition, cancellationToken));

        using HttpRequestMessage missingCourse = Authorized(
            HttpMethod.Post,
            "/api/v1/conversations",
            creator.AccessToken);
        missingCourse.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        missingCourse.Content = JsonContent.Create(new { participantUserIds = new[] { participant.UserId } });
        using HttpResponseMessage missingCourseResponse = await fixture.Client.SendAsync(missingCourse, cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingCourseResponse.StatusCode);

        using HttpRequestMessage createConversation = Authorized(
            HttpMethod.Post,
            "/api/v1/conversations",
            creator.AccessToken);
        createConversation.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        createConversation.Content = JsonContent.Create(new
        {
            participantUserIds = new[] { participant.UserId },
            courseId,
        });
        using HttpResponseMessage conversationResponse = await fixture.Client.SendAsync(
            createConversation,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, conversationResponse.StatusCode);
        Assert.True(conversationResponse.Headers.CacheControl?.NoStore);
        ApiResponse<ConversationResponse>? conversation = await conversationResponse.Content
            .ReadFromJsonAsync<ApiResponse<ConversationResponse>>(cancellationToken);
        Assert.NotNull(conversation);

        string messagesPath = $"/api/v1/conversations/{conversation.Data.Id:D}/messages";
        using HttpRequestMessage foreignMessages = Authorized(HttpMethod.Get, messagesPath, outsider.AccessToken);
        using HttpResponseMessage hidden = await fixture.Client.SendAsync(foreignMessages, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal("CONVERSATION.NOT_FOUND", await ReadProblemCodeAsync(hidden, cancellationToken));

        using HttpRequestMessage missingMessageKey = Authorized(HttpMethod.Post, messagesPath, participant.AccessToken);
        missingMessageKey.Content = JsonContent.Create(new
        {
            clientMessageId = Guid.CreateVersion7(),
            body = "Synthetic API message.",
        });
        using HttpResponseMessage messagePrecondition = await fixture.Client.SendAsync(
            missingMessageKey,
            cancellationToken);
        Assert.Equal((HttpStatusCode)428, messagePrecondition.StatusCode);

        using HttpRequestMessage createMessage = Authorized(HttpMethod.Post, messagesPath, participant.AccessToken);
        createMessage.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        createMessage.Content = JsonContent.Create(new
        {
            clientMessageId = Guid.CreateVersion7(),
            body = "Synthetic API message.",
        });
        using HttpResponseMessage messageResponse = await fixture.Client.SendAsync(createMessage, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, messageResponse.StatusCode);
        Assert.True(messageResponse.Headers.CacheControl?.NoStore);
        ApiResponse<MessageResponse>? createdMessage = await messageResponse.Content
            .ReadFromJsonAsync<ApiResponse<MessageResponse>>(cancellationToken);
        Assert.Equal(1, Assert.IsType<ApiResponse<MessageResponse>>(createdMessage).Data.Sequence);

        using HttpRequestMessage getMessages = Authorized(
            HttpMethod.Get,
            $"{messagesPath}?afterSequence=0",
            creator.AccessToken);
        using HttpResponseMessage messagesResponse = await fixture.Client.SendAsync(getMessages, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, messagesResponse.StatusCode);
        Assert.True(messagesResponse.Headers.CacheControl?.NoStore);
        ApiResponse<MessagePageResponse>? messages = await messagesResponse.Content
            .ReadFromJsonAsync<ApiResponse<MessagePageResponse>>(cancellationToken);
        Assert.Single(Assert.IsType<ApiResponse<MessagePageResponse>>(messages).Data.Items);
        Assert.Equal(1, messages.Data.LatestSequence);

        using HttpRequestMessage getNotifications = Authorized(
            HttpMethod.Get,
            "/api/v1/me/notifications?afterSequence=0",
            creator.AccessToken);
        using HttpResponseMessage notificationsResponse = await fixture.Client.SendAsync(
            getNotifications,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, notificationsResponse.StatusCode);
        Assert.True(notificationsResponse.Headers.CacheControl?.NoStore);
        ApiResponse<NotificationPageResponse>? notifications = await notificationsResponse.Content
            .ReadFromJsonAsync<ApiResponse<NotificationPageResponse>>(cancellationToken);
        NotificationResponse notification = Assert.Single(
            Assert.IsType<ApiResponse<NotificationPageResponse>>(notifications).Data.Items);
        Assert.Equal("Message", notification.Type);
        Assert.Null(notification.Body);

        using HttpRequestMessage foreignRead = Authorized(
            HttpMethod.Put,
            $"/api/v1/me/notifications/{notification.Id:D}/read",
            outsider.AccessToken);
        using HttpResponseMessage foreignReadResponse = await fixture.Client.SendAsync(foreignRead, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignReadResponse.StatusCode);
        Assert.Equal("NOTIFICATION.NOT_FOUND", await ReadProblemCodeAsync(foreignReadResponse, cancellationToken));

        using HttpRequestMessage unreadCount = Authorized(
            HttpMethod.Get,
            "/api/v1/me/notifications/unread-count",
            creator.AccessToken);
        using HttpResponseMessage unreadResponse = await fixture.Client.SendAsync(unreadCount, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, unreadResponse.StatusCode);
        Assert.True(unreadResponse.Headers.CacheControl?.NoStore);
        ApiResponse<NotificationUnreadCountResponse>? unread = await unreadResponse.Content
            .ReadFromJsonAsync<ApiResponse<NotificationUnreadCountResponse>>(cancellationToken);
        Assert.Equal(1, Assert.IsType<ApiResponse<NotificationUnreadCountResponse>>(unread).Data.Count);

        using HttpRequestMessage ownedRead = Authorized(
            HttpMethod.Put,
            $"/api/v1/me/notifications/{notification.Id:D}/read",
            creator.AccessToken);
        using HttpResponseMessage ownedReadResponse = await fixture.Client.SendAsync(ownedRead, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, ownedReadResponse.StatusCode);
        Assert.True(ownedReadResponse.Headers.CacheControl?.NoStore);

        string leavePath = $"/api/v1/conversations/{conversation.Data.Id:D}/participants/me";
        using HttpRequestMessage foreignLeave = Authorized(HttpMethod.Delete, leavePath, outsider.AccessToken);
        using HttpResponseMessage foreignLeaveResponse = await fixture.Client.SendAsync(
            foreignLeave,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignLeaveResponse.StatusCode);
        Assert.Equal("CONVERSATION.NOT_FOUND", await ReadProblemCodeAsync(foreignLeaveResponse, cancellationToken));

        using HttpRequestMessage leave = Authorized(HttpMethod.Delete, leavePath, participant.AccessToken);
        using HttpResponseMessage leaveResponse = await fixture.Client.SendAsync(leave, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, leaveResponse.StatusCode);
        Assert.True(leaveResponse.Headers.CacheControl?.NoStore);

        using HttpRequestMessage formerParticipantMessages = Authorized(
            HttpMethod.Get,
            messagesPath,
            participant.AccessToken);
        using HttpResponseMessage formerParticipantResponse = await fixture.Client.SendAsync(
            formerParticipantMessages,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, formerParticipantResponse.StatusCode);

        using HttpRequestMessage participantConversations = Authorized(
            HttpMethod.Get,
            "/api/v1/conversations",
            participant.AccessToken);
        using HttpResponseMessage participantConversationsResponse = await fixture.Client.SendAsync(
            participantConversations,
            cancellationToken);
        ApiResponse<ConversationPageResponse>? participantPage = await participantConversationsResponse.Content
            .ReadFromJsonAsync<ApiResponse<ConversationPageResponse>>(cancellationToken);
        Assert.DoesNotContain(
            Assert.IsType<ApiResponse<ConversationPageResponse>>(participantPage).Data.Items,
            item => item.Id == conversation.Data.Id);
    }

    [Fact]
    public async Task AnnouncementCrudRequiresCourseScopeIdempotencyAndNoStore()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser owner = await CreateUserAsync(
            "communications-http-announcement-owner",
            cancellationToken,
            DorosakIdentityConstants.TeacherRole);
        SignedInUser foreignTeacher = await CreateUserAsync(
            "communications-http-announcement-foreign",
            cancellationToken,
            DorosakIdentityConstants.TeacherRole);
        Guid courseId;
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            Course course = Course.Create(owner.UserId, "en", DateTimeOffset.UtcNow);
            dbContext.Set<Course>().Add(course);
            await dbContext.SaveChangesAsync(cancellationToken);
            courseId = course.Id;
        }

        string path = $"/api/v1/instructor/courses/{courseId:D}/announcements";
        using HttpRequestMessage missingKey = Authorized(HttpMethod.Post, path, owner.AccessToken);
        missingKey.Content = JsonContent.Create(new { title = "Course notice", body = "Bounded notice body." });
        using HttpResponseMessage missingKeyResponse = await fixture.Client.SendAsync(missingKey, cancellationToken);
        Assert.Equal((HttpStatusCode)428, missingKeyResponse.StatusCode);

        string createKey = Guid.CreateVersion7().ToString("N");
        using HttpRequestMessage create = Authorized(HttpMethod.Post, path, owner.AccessToken);
        create.Headers.Add("Idempotency-Key", createKey);
        create.Content = JsonContent.Create(new { title = "Course notice", body = "Bounded notice body." });
        using HttpResponseMessage createResponse = await fixture.Client.SendAsync(create, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.True(createResponse.Headers.CacheControl?.NoStore);
        ApiResponse<AnnouncementResponse>? created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<AnnouncementResponse>>(cancellationToken);
        AnnouncementResponse announcement = Assert.IsType<ApiResponse<AnnouncementResponse>>(created).Data;
        Assert.Equal(0, announcement.TargetCount);

        using HttpRequestMessage foreignGet = Authorized(HttpMethod.Get, path, foreignTeacher.AccessToken);
        using HttpResponseMessage foreignResponse = await fixture.Client.SendAsync(foreignGet, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal("ANNOUNCEMENT.NOT_FOUND", await ReadProblemCodeAsync(foreignResponse, cancellationToken));

        using HttpRequestMessage list = Authorized(HttpMethod.Get, path, owner.AccessToken);
        using HttpResponseMessage listResponse = await fixture.Client.SendAsync(list, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.True(listResponse.Headers.CacheControl?.NoStore);
        ApiResponse<AnnouncementPageResponse>? page = await listResponse.Content
            .ReadFromJsonAsync<ApiResponse<AnnouncementPageResponse>>(cancellationToken);
        Assert.Contains(
            Assert.IsType<ApiResponse<AnnouncementPageResponse>>(page).Data.Items,
            item => item.Id == announcement.Id);

        using HttpRequestMessage update = Authorized(
            HttpMethod.Put,
            $"{path}/{announcement.Id:D}",
            owner.AccessToken);
        update.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        update.Content = JsonContent.Create(new { title = "Revised notice", body = "Revised bounded body." });
        using HttpResponseMessage updateResponse = await fixture.Client.SendAsync(update, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(updateResponse.Headers.CacheControl?.NoStore);
        ApiResponse<AnnouncementResponse>? updated = await updateResponse.Content
            .ReadFromJsonAsync<ApiResponse<AnnouncementResponse>>(cancellationToken);
        Assert.Equal(2, Assert.IsType<ApiResponse<AnnouncementResponse>>(updated).Data.Version);

        using HttpRequestMessage delete = Authorized(
            HttpMethod.Delete,
            $"{path}/{announcement.Id:D}",
            owner.AccessToken);
        using HttpResponseMessage deleteResponse = await fixture.Client.SendAsync(delete, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.True(deleteResponse.Headers.CacheControl?.NoStore);
    }

    private async Task<SignedInUser> CreateUserAsync(
        string prefix,
        CancellationToken cancellationToken,
        string role = DorosakIdentityConstants.StudentRole)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await manager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        Result<SignInResponse> signIn = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.71", "communications API test", "en")),
            cancellationToken);
        Assert.True(signIn.IsSuccess);
        return new SignedInUser(user.Id, signIn.Value.Session!.AccessToken);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Assert.IsType<string>(document.RootElement.GetProperty("code").GetString());
    }

    private sealed record SignedInUser(Guid UserId, string AccessToken);
}
