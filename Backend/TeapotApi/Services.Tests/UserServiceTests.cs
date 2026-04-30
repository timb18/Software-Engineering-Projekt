using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Services.Tests;

[TestFixture]
public class UserServiceTests
{
    private TeapotDbContext _dbContext = null!;
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TeapotDbContext(options);
        _service = BuildService(_dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private static UserService BuildService(TeapotDbContext db) => new(
        new UserRepository(db),
        new OrganizationRepository(db),
        new MembershipRepository(db),
        new WorkProfileRepository(db),
        db);

    // ── EnsureUserAsync – new user ────────────────────────────────────────────

    [Test]
    public async Task EnsureUserAsync_Returns_Non_Empty_Ids_For_New_Email()
    {
        var (userId, workProfileId) = await _service.EnsureUserAsync("new@example.com");

        Assert.That(userId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(workProfileId, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task EnsureUserAsync_Creates_User_Row_In_Database()
    {
        var (userId, _) = await _service.EnsureUserAsync("created@example.com");

        var user = await _dbContext.Set<User>().FindAsync(userId);
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Email, Is.EqualTo("created@example.com"));
    }

    [Test]
    public async Task EnsureUserAsync_Creates_WorkProfile_Row_In_Database()
    {
        var (_, workProfileId) = await _service.EnsureUserAsync("profile@example.com");

        var workProfile = await _dbContext.Set<WorkProfile>().FindAsync(workProfileId);
        Assert.That(workProfile, Is.Not.Null);
    }

    [Test]
    public async Task EnsureUserAsync_Creates_Personal_Organization_For_New_User()
    {
        var (_, workProfileId) = await _service.EnsureUserAsync("org@example.com");

        var workProfile = await _dbContext.Set<WorkProfile>()
            .Include(wp => wp.Membership)
            .ThenInclude(m => m.Organization)
            .FirstAsync(wp => wp.Id == workProfileId);

        Assert.That(workProfile.Membership.Organization.Name, Does.Contain("org@example.com"));
    }

    [Test]
    public async Task EnsureUserAsync_Sets_Organizer_Role_On_Personal_Membership()
    {
        var (_, workProfileId) = await _service.EnsureUserAsync("organizer@example.com");

        var workProfile = await _dbContext.Set<WorkProfile>()
            .Include(wp => wp.Membership)
            .FirstAsync(wp => wp.Id == workProfileId);

        Assert.That(workProfile.Membership.Role, Is.EqualTo(ERole.Organizer));
    }

    // ── EnsureUserAsync – returning user ──────────────────────────────────────

    [Test]
    public async Task EnsureUserAsync_Returns_Same_Ids_On_Second_Call_For_Same_Email()
    {
        var (userId1, workProfileId1) = await _service.EnsureUserAsync("returning@example.com");
        var (userId2, workProfileId2) = await _service.EnsureUserAsync("returning@example.com");

        Assert.That(userId2, Is.EqualTo(userId1));
        Assert.That(workProfileId2, Is.EqualTo(workProfileId1));
    }

    [Test]
    public async Task EnsureUserAsync_Does_Not_Create_Duplicate_User_Rows()
    {
        await _service.EnsureUserAsync("dup@example.com");
        await _service.EnsureUserAsync("dup@example.com");

        var count = _dbContext.Set<User>().Count(u => u.Email == "dup@example.com");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task EnsureUserAsync_Does_Not_Create_Duplicate_WorkProfile_Rows()
    {
        await _service.EnsureUserAsync("dup-wp@example.com");
        await _service.EnsureUserAsync("dup-wp@example.com");

        var userId = _dbContext.Set<User>().First(u => u.Email == "dup-wp@example.com").Id;
        var profileCount = _dbContext.Set<WorkProfile>()
            .Include(wp => wp.Membership)
            .Count(wp => wp.Membership.UserId == userId);

        Assert.That(profileCount, Is.EqualTo(1));
    }

    // ── EnsureUserAsync – different users are independent ────────────────────

    [Test]
    public async Task EnsureUserAsync_Creates_Separate_WorkProfiles_For_Different_Emails()
    {
        var (_, profileA) = await _service.EnsureUserAsync("a@example.com");
        var (_, profileB) = await _service.EnsureUserAsync("b@example.com");

        Assert.That(profileA, Is.Not.EqualTo(profileB));
    }

    [Test]
    public async Task EnsureUserAsync_Reuses_User_When_Auth_Subject_Matches_After_Email_Change()
    {
        var (firstUserId, firstProfileId) = await _service.EnsureUserAsync(
            "before@example.com",
            "auth0|abc",
            "Anna Before",
            "https://example.com/a.png");

        var (secondUserId, secondProfileId) = await _service.EnsureUserAsync(
            "after@example.com",
            "auth0|abc",
            "Anna After",
            "https://example.com/b.png");

        var user = await _dbContext.Users.FindAsync(secondUserId);

        Assert.Multiple(() =>
        {
            Assert.That(secondUserId, Is.EqualTo(firstUserId));
            Assert.That(secondProfileId, Is.EqualTo(firstProfileId));
            Assert.That(user!.Email, Is.EqualTo("before@example.com"));
            Assert.That(user.DisplayName, Is.EqualTo("Anna Before"));
            Assert.That(user.ProfileImageUrl, Is.EqualTo("https://example.com/a.png"));
            Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task UpdateProfileAsync_Persists_Profile_Fields()
    {
        var (userId, _) = await _service.EnsureUserAsync("profile-update@example.com");

        var updated = await _service.UpdateProfileAsync(
            userId,
            new UpdateUserProfileCommand(
                "Updated Name",
                "updated@example.com",
                "https://example.com/avatar.png",
                "Europe/Paris"));

        var user = await _dbContext.Users.FindAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(updated.DisplayName, Is.EqualTo("Updated Name"));
            Assert.That(updated.Email, Is.EqualTo("updated@example.com"));
            Assert.That(updated.ProfileImageUrl, Is.EqualTo("https://example.com/avatar.png"));
            Assert.That(updated.Timezone, Is.EqualTo("Europe/Paris"));
            Assert.That(user!.EditedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task EnsureUserAsync_Does_Not_Overwrite_Existing_Profile_Customizations()
    {
        var (userId, _) = await _service.EnsureUserAsync(
            "custom@example.com",
            "auth0|custom",
            "Auth Name",
            "https://example.com/auth.png");

        await _service.UpdateProfileAsync(
            userId,
            new UpdateUserProfileCommand(
                "Custom Name",
                "custom@example.com",
                "https://example.com/custom.png",
                "Europe/Berlin"));

        await _service.EnsureUserAsync(
            "custom@example.com",
            "auth0|custom",
            "Auth Name Reloaded",
            "https://example.com/auth-reloaded.png");

        var user = await _dbContext.Users.FindAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(user!.DisplayName, Is.EqualTo("Custom Name"));
            Assert.That(user.ProfileImageUrl, Is.EqualTo("https://example.com/custom.png"));
        });
    }

    [Test]
    public void UpdateProfileAsync_Rejects_Empty_Display_Name()
    {
        var (userId, _) = _service.EnsureUserAsync("empty-name@example.com").GetAwaiter().GetResult();

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.UpdateProfileAsync(
                userId,
                new UpdateUserProfileCommand("", "valid@example.com", null, "Europe/Berlin")));
    }

    [Test]
    public void UpdateProfileAsync_Rejects_Invalid_Email()
    {
        var (userId, _) = _service.EnsureUserAsync("invalid-email-start@example.com").GetAwaiter().GetResult();

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.UpdateProfileAsync(
                userId,
                new UpdateUserProfileCommand("Valid Name", "invalid-email", null, "Europe/Berlin")));
    }
}
