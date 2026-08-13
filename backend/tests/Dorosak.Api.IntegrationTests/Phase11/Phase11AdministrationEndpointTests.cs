using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class Phase11AdministrationEndpointTests(ApiFixture fixture)
{
    private const string TestPassword = "correct horse battery staple";
    private static readonly string[] SupportedPageSlugs = ["terms", "privacy", "contact", "about"];
    private static int _clientIpSequence;

    [Fact]
    public async Task AdminEndpoints_RequireAuthenticationPermissionsMfaAndAuditReason()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpResponseMessage anonymous = await fixture.Client.GetAsync(
            "/api/v1/admin/cms",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("AUTH.AUTHENTICATION_REQUIRED", await ReadProblemCodeAsync(anonymous, cancellationToken));

        SignedInUser student = await CreateUserAsync("phase11-access-student", cancellationToken);
        using HttpRequestMessage studentCmsRequest = Authorized(HttpMethod.Get, "/api/v1/admin/cms", student.AccessToken);
        using HttpResponseMessage studentCms = await fixture.Client.SendAsync(studentCmsRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, studentCms.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(studentCms, cancellationToken));

        using HttpRequestMessage studentAuditRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/audit-logs?limit=1",
            student.AccessToken,
            "Phase 11 student authorization verification.");
        using HttpResponseMessage studentAudit = await fixture.Client.SendAsync(studentAuditRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, studentAudit.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(studentAudit, cancellationToken));

        await AddAdminRoleAsync(student.UserId);
        using HttpRequestMessage passwordAdminCmsRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/cms",
            student.AccessToken);
        using HttpResponseMessage passwordAdminCms = await fixture.Client.SendAsync(
            passwordAdminCmsRequest,
            cancellationToken);
        JsonElement passwordAdminCmsData = await ReadOkDataAsync(passwordAdminCms, cancellationToken);
        AssertNoStore(passwordAdminCms);
        AssertAdminCmsDto(passwordAdminCmsData);

        using HttpRequestMessage passwordAdminAuditRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/audit-logs?limit=1",
            student.AccessToken,
            "Phase 11 password-only administrator verification.");
        using HttpResponseMessage passwordAdminAudit = await fixture.Client.SendAsync(
            passwordAdminAuditRequest,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, passwordAdminAudit.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(passwordAdminAudit, cancellationToken));

        SignedInUser admin = await CreateMfaAdminAsync("phase11-access-admin", cancellationToken);
        using HttpRequestMessage settingsRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/settings",
            admin.AccessToken);
        using HttpResponseMessage settingsResponse = await fixture.Client.SendAsync(settingsRequest, cancellationToken);
        JsonElement settings = await ReadOkDataAsync(settingsResponse, cancellationToken);
        AssertNoStore(settingsResponse);
        AssertProperties(
            settings,
            "featuredCourseLimit",
            "showPortfolioNotice",
            "noticeAr",
            "noticeEn",
            "version",
            "updatedAt");

        using HttpRequestMessage missingReasonRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/audit-logs?limit=1",
            admin.AccessToken);
        using HttpResponseMessage missingReason = await fixture.Client.SendAsync(
            missingReasonRequest,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, missingReason.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(missingReason, cancellationToken));

        using HttpRequestMessage shortReasonRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/audit-logs?limit=1",
            admin.AccessToken,
            "short");
        using HttpResponseMessage shortReason = await fixture.Client.SendAsync(shortReasonRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, shortReason.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(shortReason, cancellationToken));

        using HttpRequestMessage validReasonRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/audit-logs?action=phase11.no-record&limit=1",
            admin.AccessToken,
            "Phase 11 MFA administrator policy verification.");
        using HttpResponseMessage validReason = await fixture.Client.SendAsync(validReasonRequest, cancellationToken);
        JsonElement auditPage = await ReadOkDataAsync(validReason, cancellationToken);
        AssertNoStore(validReason);
        AssertAuditPageDto(auditPage);
        Assert.Empty(auditPage.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task PageLifecycle_KeepsDraftsPrivateAndReturnsLocalizedPublishedContent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser admin = await CreateMfaAdminAsync("phase11-cms-admin", cancellationToken);

        using HttpRequestMessage cmsRequest = Authorized(HttpMethod.Get, "/api/v1/admin/cms", admin.AccessToken);
        using HttpResponseMessage cmsResponse = await fixture.Client.SendAsync(cmsRequest, cancellationToken);
        JsonElement cms = await ReadOkDataAsync(cmsResponse, cancellationToken);
        AssertNoStore(cmsResponse);
        AssertAdminCmsDto(cms);

        PageSlot? slot = FindUnpublishedPageSlot(cms.GetProperty("pages"));
        if (slot is null)
        {
            Assert.Skip("All supported CMS page slugs are already published in the shared database.");
        }
        PageSlot selected = slot!;
        string marker = Guid.CreateVersion7().ToString("N");
        string titleArV1 = $"phase11-ar-title-v1-{marker}";
        string titleEnV1 = $"phase11-en-title-v1-{marker}";
        string bodyArV1 = $"phase11-ar-body-v1-{marker}";
        string bodyEnV1 = $"phase11-en-body-v1-{marker}";
        string draftReason = $"Phase 11 initial page draft {marker}.";

        using HttpRequestMessage draftRequest = Authorized(
            HttpMethod.Put,
            $"/api/v1/admin/cms/pages/{selected.Slug}/draft",
            admin.AccessToken,
            draftReason);
        draftRequest.Content = JsonContent.Create(new
        {
            expectedVersion = selected.ExpectedVersion,
            titleAr = titleArV1,
            titleEn = titleEnV1,
            bodyAr = bodyArV1,
            bodyEn = bodyEnV1,
        });
        using HttpResponseMessage draftResponse = await fixture.Client.SendAsync(draftRequest, cancellationToken);
        JsonElement draft = await ReadOkDataAsync(draftResponse, cancellationToken);
        AssertNoStore(draftResponse);
        AssertCmsPageDto(draft);
        int firstVersion = selected.ExpectedVersion + 1;
        Assert.Equal(selected.Slug, draft.GetProperty("slug").GetString());
        Assert.Equal(firstVersion, draft.GetProperty("currentVersion").GetInt32());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("publishedVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("published").ValueKind);
        JsonElement firstRevision = draft.GetProperty("draft");
        Assert.Equal(firstVersion, firstRevision.GetProperty("version").GetInt32());
        Assert.Equal(admin.UserId, firstRevision.GetProperty("createdByUserId").GetGuid());

        using HttpRequestMessage privatePageRequest = PublicGet(
            $"/api/v1/pages/{selected.Slug}",
            "en-US");
        using HttpResponseMessage privatePage = await fixture.Client.SendAsync(privatePageRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, privatePage.StatusCode);
        Assert.Equal("CMS.PAGE_NOT_FOUND", await ReadProblemCodeAsync(privatePage, cancellationToken));

        using HttpRequestMessage publishRequest = Authorized(
            HttpMethod.Post,
            $"/api/v1/admin/cms/pages/{selected.Slug}/publish",
            admin.AccessToken,
            $"Phase 11 page publication {marker}.");
        publishRequest.Content = JsonContent.Create(new { expectedVersion = firstVersion });
        using HttpResponseMessage publishResponse = await fixture.Client.SendAsync(publishRequest, cancellationToken);
        JsonElement published = await ReadOkDataAsync(publishResponse, cancellationToken);
        AssertNoStore(publishResponse);
        AssertCmsPageDto(published);
        Assert.Equal(firstVersion, published.GetProperty("publishedVersion").GetInt32());
        Assert.Equal(firstVersion, published.GetProperty("published").GetProperty("version").GetInt32());

        using HttpRequestMessage englishRequest = PublicGet($"/api/v1/pages/{selected.Slug}", "en-US");
        using HttpResponseMessage englishResponse = await fixture.Client.SendAsync(englishRequest, cancellationToken);
        JsonElement english = await ReadOkDataAsync(englishResponse, cancellationToken);
        AssertPublicPageDto(english);
        Assert.Equal("en", english.GetProperty("locale").GetString());
        Assert.Equal(titleEnV1, english.GetProperty("title").GetString());
        Assert.Equal(bodyEnV1, english.GetProperty("body").GetString());
        Assert.Equal(firstVersion, english.GetProperty("version").GetInt32());

        using HttpRequestMessage arabicRequest = PublicGet($"/api/v1/pages/{selected.Slug}", "ar-EG");
        using HttpResponseMessage arabicResponse = await fixture.Client.SendAsync(arabicRequest, cancellationToken);
        JsonElement arabic = await ReadOkDataAsync(arabicResponse, cancellationToken);
        AssertPublicPageDto(arabic);
        Assert.Equal("ar", arabic.GetProperty("locale").GetString());
        Assert.Equal(titleArV1, arabic.GetProperty("title").GetString());
        Assert.Equal(bodyArV1, arabic.GetProperty("body").GetString());
        Assert.Equal(firstVersion, arabic.GetProperty("version").GetInt32());

        string titleEnV2 = $"phase11-en-title-v2-{marker}";
        string bodyEnV2 = $"phase11-en-body-v2-{marker}";
        using HttpRequestMessage secondDraftRequest = Authorized(
            HttpMethod.Put,
            $"/api/v1/admin/cms/pages/{selected.Slug}/draft",
            admin.AccessToken,
            $"Phase 11 private second page draft {marker}.");
        secondDraftRequest.Content = JsonContent.Create(new
        {
            expectedVersion = firstVersion,
            titleAr = $"phase11-ar-title-v2-{marker}",
            titleEn = titleEnV2,
            bodyAr = $"phase11-ar-body-v2-{marker}",
            bodyEn = bodyEnV2,
        });
        using HttpResponseMessage secondDraftResponse = await fixture.Client.SendAsync(
            secondDraftRequest,
            cancellationToken);
        JsonElement secondDraft = await ReadOkDataAsync(secondDraftResponse, cancellationToken);
        AssertNoStore(secondDraftResponse);
        AssertCmsPageDto(secondDraft);
        Assert.Equal(firstVersion + 1, secondDraft.GetProperty("currentVersion").GetInt32());
        Assert.Equal(firstVersion, secondDraft.GetProperty("publishedVersion").GetInt32());
        Assert.Equal(firstVersion + 1, secondDraft.GetProperty("draft").GetProperty("version").GetInt32());
        Assert.Equal(firstVersion, secondDraft.GetProperty("published").GetProperty("version").GetInt32());

        using HttpRequestMessage stillPublishedRequest = PublicGet(
            $"/api/v1/pages/{selected.Slug}?draft-probe={marker}",
            "en-US");
        using HttpResponseMessage stillPublishedResponse = await fixture.Client.SendAsync(
            stillPublishedRequest,
            cancellationToken);
        JsonElement stillPublished = await ReadOkDataAsync(stillPublishedResponse, cancellationToken);
        AssertPublicPageDto(stillPublished);
        Assert.Equal(firstVersion, stillPublished.GetProperty("version").GetInt32());
        Assert.Equal(titleEnV1, stillPublished.GetProperty("title").GetString());
        Assert.Equal(bodyEnV1, stillPublished.GetProperty("body").GetString());
        Assert.NotEqual(titleEnV2, stillPublished.GetProperty("title").GetString());
        Assert.NotEqual(bodyEnV2, stillPublished.GetProperty("body").GetString());
    }

    [Fact]
    public async Task SettingsUpdate_RejectsStaleVersionInvalidatesPublicCacheAndAuditsAuditReads()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser admin = await CreateMfaAdminAsync("phase11-settings-admin", cancellationToken);
        string marker = Guid.CreateVersion7().ToString("N");

        using HttpRequestMessage currentRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/settings",
            admin.AccessToken);
        using HttpResponseMessage currentResponse = await fixture.Client.SendAsync(currentRequest, cancellationToken);
        JsonElement current = await ReadOkDataAsync(currentResponse, cancellationToken);
        AssertNoStore(currentResponse);
        AssertProperties(
            current,
            "featuredCourseLimit",
            "showPortfolioNotice",
            "noticeAr",
            "noticeEn",
            "version",
            "updatedAt");
        long expectedVersion = current.GetProperty("version").GetInt64();
        int featuredCourseLimit = current.GetProperty("featuredCourseLimit").GetInt32() % 12 + 1;

        using HttpRequestMessage cachedPublicRequest = PublicGet("/api/v1/portfolio-settings", "en-US");
        using HttpResponseMessage cachedPublicResponse = await fixture.Client.SendAsync(
            cachedPublicRequest,
            cancellationToken);
        JsonElement cachedPublic = await ReadOkDataAsync(cachedPublicResponse, cancellationToken);
        AssertPublicSettingsDto(cachedPublic);

        string noticeAr = $"phase11-ar-settings-{marker}";
        string noticeEn = $"phase11-en-settings-{marker}";
        string updateReason = $"Phase 11 settings cache update {marker}.";
        using HttpRequestMessage updateRequest = Authorized(
            HttpMethod.Put,
            "/api/v1/admin/settings",
            admin.AccessToken,
            updateReason);
        updateRequest.Content = JsonContent.Create(new
        {
            featuredCourseLimit,
            showPortfolioNotice = true,
            noticeAr,
            noticeEn,
            expectedVersion,
        });
        using HttpResponseMessage updateResponse = await fixture.Client.SendAsync(updateRequest, cancellationToken);
        JsonElement updated = await ReadOkDataAsync(updateResponse, cancellationToken);
        AssertNoStore(updateResponse);
        Assert.Equal(expectedVersion + 1, updated.GetProperty("version").GetInt64());
        Assert.Equal(featuredCourseLimit, updated.GetProperty("featuredCourseLimit").GetInt32());
        Assert.True(updated.GetProperty("showPortfolioNotice").GetBoolean());
        Assert.Equal(noticeAr, updated.GetProperty("noticeAr").GetString());
        Assert.Equal(noticeEn, updated.GetProperty("noticeEn").GetString());

        using HttpRequestMessage refreshedPublicRequest = PublicGet("/api/v1/portfolio-settings", "en-US");
        using HttpResponseMessage refreshedPublicResponse = await fixture.Client.SendAsync(
            refreshedPublicRequest,
            cancellationToken);
        JsonElement refreshedPublic = await ReadOkDataAsync(refreshedPublicResponse, cancellationToken);
        AssertPublicSettingsDto(refreshedPublic);
        Assert.Equal("en", refreshedPublic.GetProperty("locale").GetString());
        Assert.Equal(featuredCourseLimit, refreshedPublic.GetProperty("featuredCourseLimit").GetInt32());
        Assert.True(refreshedPublic.GetProperty("showPortfolioNotice").GetBoolean());
        Assert.Equal(noticeEn, refreshedPublic.GetProperty("portfolioNotice").GetString());

        using HttpRequestMessage staleRequest = Authorized(
            HttpMethod.Put,
            "/api/v1/admin/settings",
            admin.AccessToken,
            $"Phase 11 stale settings update {marker}.");
        staleRequest.Content = JsonContent.Create(new
        {
            featuredCourseLimit = featuredCourseLimit % 12 + 1,
            showPortfolioNotice = true,
            noticeAr = $"stale-ar-{marker}",
            noticeEn = $"stale-en-{marker}",
            expectedVersion,
        });
        using HttpResponseMessage staleResponse = await fixture.Client.SendAsync(staleRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        AssertNoStore(staleResponse);
        Assert.Equal("SETTINGS.VERSION_CONFLICT", await ReadProblemCodeAsync(staleResponse, cancellationToken));

        string firstAuditReadReason = $"Phase 11 settings audit read {marker}.";
        using HttpRequestMessage settingsAuditRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/audit-logs?action=settings.portfolio-updated&limit=100",
            admin.AccessToken,
            firstAuditReadReason);
        using HttpResponseMessage settingsAuditResponse = await fixture.Client.SendAsync(
            settingsAuditRequest,
            cancellationToken);
        JsonElement settingsAuditPage = await ReadOkDataAsync(settingsAuditResponse, cancellationToken);
        AssertNoStore(settingsAuditResponse);
        AssertAuditPageDto(settingsAuditPage);
        JsonElement settingsAudit = settingsAuditPage.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("actorUserId").GetGuid() == admin.UserId &&
            string.Equals(item.GetProperty("reason").GetString(), updateReason, StringComparison.Ordinal));
        Assert.Equal("settings.portfolio-updated", settingsAudit.GetProperty("action").GetString());
        Assert.Equal("PortfolioSettings", settingsAudit.GetProperty("targetType").GetString());
        Assert.Equal("Succeeded", settingsAudit.GetProperty("result").GetString());

        using HttpRequestMessage selfAuditRequest = Authorized(
            HttpMethod.Get,
            "/api/v1/admin/audit-logs?action=audit.logs-read&limit=100",
            admin.AccessToken,
            $"Phase 11 self-audit visibility read {marker}.");
        using HttpResponseMessage selfAuditResponse = await fixture.Client.SendAsync(
            selfAuditRequest,
            cancellationToken);
        JsonElement selfAuditPage = await ReadOkDataAsync(selfAuditResponse, cancellationToken);
        AssertNoStore(selfAuditResponse);
        AssertAuditPageDto(selfAuditPage);
        JsonElement selfAudit = selfAuditPage.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("actorUserId").GetGuid() == admin.UserId &&
            string.Equals(item.GetProperty("reason").GetString(), firstAuditReadReason, StringComparison.Ordinal));
        Assert.Equal("audit.logs-read", selfAudit.GetProperty("action").GetString());
        Assert.Equal("AuditLog", selfAudit.GetProperty("targetType").GetString());
        Assert.Equal(admin.UserId, selfAudit.GetProperty("targetId").GetGuid());
        Assert.Equal("Succeeded", selfAudit.GetProperty("result").GetString());
    }

    private async Task<SignedInUser> CreateUserAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user, TestPassword)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, DorosakIdentityConstants.StudentRole)).Succeeded);

        Result<SignInResponse> signIn = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new SignInCommand(user.Email!, TestPassword, CreateIdentityContext()),
            cancellationToken);
        Assert.True(signIn.IsSuccess);
        AuthenticatedSessionResponse session = Assert.IsType<AuthenticatedSessionResponse>(signIn.Value.Session);
        return new SignedInUser(user.Id, session.AccessToken);
    }

    private async Task<SignedInUser> CreateMfaAdminAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user, TestPassword)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, DorosakIdentityConstants.StudentRole)).Succeeded);

        IdentityRequestContext context = CreateIdentityContext();
        Result<SignInResponse> initialSignIn = await sender.Send(
            new SignInCommand(user.Email!, TestPassword, context),
            cancellationToken);
        Assert.True(initialSignIn.IsSuccess);
        AuthenticatedSessionResponse initialSession = Assert.IsType<AuthenticatedSessionResponse>(
            initialSignIn.Value.Session);
        Result<MfaSetupResponse> setup = await sender.Send(
            new SetupMfaCommand(user.Id, initialSession.Identity.SessionId),
            cancellationToken);
        Assert.True(setup.IsSuccess);
        string setupCode = new Totp(Base32Encoding.ToBytes(setup.Value.Secret)).ComputeTotp();
        Result<MfaConfirmationResponse> confirmation = await sender.Send(
            new ConfirmMfaCommand(user.Id, initialSession.Identity.SessionId, setupCode),
            cancellationToken);
        Assert.True(confirmation.IsSuccess);
        Assert.True((await userManager.AddToRoleAsync(user, DorosakIdentityConstants.AdminRole)).Succeeded);

        Result<SignInResponse> challenged = await sender.Send(
            new SignInCommand(user.Email!, TestPassword, context),
            cancellationToken);
        Assert.True(challenged.IsSuccess);
        Assert.Equal("mfaRequired", challenged.Value.Outcome);
        Result<AuthenticatedSessionResponse> authenticated = await sender.Send(
            new CompleteMfaRecoveryCommand(
                Assert.IsType<string>(challenged.Value.ChallengeToken),
                confirmation.Value.RecoveryCodes[0],
                context),
            cancellationToken);
        Assert.True(authenticated.IsSuccess);
        return new SignedInUser(user.Id, authenticated.Value.AccessToken);
    }

    private async Task AddAdminRoleAsync(Guid userId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(await userManager.FindByIdAsync(userId.ToString("D")));
        Assert.True((await userManager.AddToRoleAsync(user, DorosakIdentityConstants.AdminRole)).Succeeded);
    }

    private static IdentityRequestContext CreateIdentityContext()
    {
        int lastOctet = 20 + Interlocked.Increment(ref _clientIpSequence) % 220;
        return new IdentityRequestContext(
            $"198.51.100.{lastOctet}",
            "Phase 11 administration API test",
            "en");
    }

    private static HttpRequestMessage Authorized(
        HttpMethod method,
        string path,
        string accessToken,
        string? auditReason = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (auditReason is not null)
        {
            request.Headers.Add("X-Audit-Reason", auditReason);
        }
        return request;
    }

    private static HttpRequestMessage PublicGet(string path, string locale)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.AcceptLanguage.ParseAdd(locale);
        return request;
    }

    private static PageSlot? FindUnpublishedPageSlot(JsonElement pages)
    {
        Dictionary<string, JsonElement> pagesBySlug = pages
            .EnumerateArray()
            .ToDictionary(
                page => Assert.IsType<string>(page.GetProperty("slug").GetString()),
                page => page.Clone(),
                StringComparer.Ordinal);
        foreach (string slug in SupportedPageSlugs)
        {
            if (!pagesBySlug.TryGetValue(slug, out JsonElement page))
            {
                return new PageSlot(slug, 0);
            }
            if (page.GetProperty("publishedVersion").ValueKind == JsonValueKind.Null)
            {
                return new PageSlot(slug, page.GetProperty("currentVersion").GetInt32());
            }
        }
        return null;
    }

    private static async Task<JsonElement> ReadOkDataAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string payload = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
        using JsonDocument document = JsonDocument.Parse(payload);
        AssertProperties(document.RootElement, "data");
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Assert.IsType<string>(document.RootElement.GetProperty("code").GetString());
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    private static void AssertAdminCmsDto(JsonElement cms)
    {
        AssertProperties(cms, "pages", "faqs");
        foreach (JsonElement page in cms.GetProperty("pages").EnumerateArray())
        {
            AssertCmsPageDto(page);
        }
        foreach (JsonElement faq in cms.GetProperty("faqs").EnumerateArray())
        {
            AssertProperties(
                faq,
                "id",
                "displayOrder",
                "currentVersion",
                "publishedVersion",
                "draft",
                "published",
                "updatedAt",
                "publishedAt");
            AssertFaqRevisionDto(faq.GetProperty("draft"));
            AssertFaqRevisionDto(faq.GetProperty("published"));
        }
    }

    private static void AssertCmsPageDto(JsonElement page)
    {
        AssertProperties(
            page,
            "id",
            "slug",
            "currentVersion",
            "publishedVersion",
            "draft",
            "published",
            "updatedAt",
            "publishedAt");
        AssertPageRevisionDto(page.GetProperty("draft"));
        AssertPageRevisionDto(page.GetProperty("published"));
    }

    private static void AssertPageRevisionDto(JsonElement revision)
    {
        if (revision.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        AssertProperties(
            revision,
            "version",
            "titleAr",
            "titleEn",
            "bodyAr",
            "bodyEn",
            "createdByUserId",
            "createdAt");
    }

    private static void AssertFaqRevisionDto(JsonElement revision)
    {
        if (revision.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        AssertProperties(
            revision,
            "version",
            "questionAr",
            "questionEn",
            "answerAr",
            "answerEn",
            "createdByUserId",
            "createdAt");
    }

    private static void AssertPublicPageDto(JsonElement page) =>
        AssertProperties(page, "slug", "locale", "title", "body", "version", "publishedAt");

    private static void AssertPublicSettingsDto(JsonElement settings) =>
        AssertProperties(
            settings,
            "locale",
            "featuredCourseLimit",
            "showPortfolioNotice",
            "portfolioNotice");

    private static void AssertAuditPageDto(JsonElement auditPage)
    {
        AssertProperties(auditPage, "items", "nextCursor", "hasMore");
        foreach (JsonElement item in auditPage.GetProperty("items").EnumerateArray())
        {
            AssertProperties(
                item,
                "id",
                "actorUserId",
                "action",
                "targetType",
                "targetId",
                "result",
                "reason",
                "occurredAt");
        }
    }

    private static void AssertProperties(JsonElement value, params string[] expected)
    {
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        string[] actual = value.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] orderedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(orderedExpected, actual);
    }

    private sealed record SignedInUser(Guid UserId, string AccessToken);

    private sealed record PageSlot(string Slug, int ExpectedVersion);
}
