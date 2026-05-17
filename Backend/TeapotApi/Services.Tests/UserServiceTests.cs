using Auth0.ManagementApi;
using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Services.Tests.Fakes;
using Organization = DataAccess.Models.Organization;

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
        new WorkProfileRepository(db),
        new UnitOfWork(db),
        new FakeManagementApiClient([
            new UserResponseSchema { UserId = "a", Email = "new@example.com" },
            new UserResponseSchema { UserId = "b", Email = "created@example.com" },
            new UserResponseSchema { UserId = "c", Email = "profile@example.com" },
            new UserResponseSchema { UserId = "d", Email = "organizer@example.com" },
            new UserResponseSchema { UserId = "e", Email = "returning@example.com" },
            new UserResponseSchema { UserId = "f", Email = "dup@example.com" },
            new UserResponseSchema { UserId = "g", Email = "dup-wp@example.com" },
            new UserResponseSchema { UserId = "h", Email = "a@example.com" },
            new UserResponseSchema { UserId = "i", Email = "b@example.com" },
            new UserResponseSchema { UserId = "j", Email = "profile-update@example.com" },
            new UserResponseSchema { UserId = "k", Email = "empty-name@example.com" },
            new UserResponseSchema { UserId = "l", Email = "invalid-email-start@example.com" }
        ]),
        new Auth0Config("", "", "", "", "", ""));

    // ── EnsureUserAsync – new user ────────────────────────────────────────────

    [Test]
    public async Task EnsureUserAsync_Returns_UserId_And_No_WorkProfile_For_New_Email()
    {
        var (userId, workProfileId) = await _service.EnsureUserAsync("new@example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(userId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(workProfileId, Is.Null);
        }
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
    public async Task EnsureUserAsync_Does_Not_Create_WorkProfile_Row_For_New_User()
    {
        var (_, workProfileId) = await _service.EnsureUserAsync("profile@example.com");

        Assert.Multiple(() =>
        {
            Assert.That(workProfileId, Is.Null);
            Assert.That(_dbContext.Set<WorkProfile>().Any(), Is.False);
        });
    }

    [Test]
    public async Task EnsureUserAsync_Does_Not_Create_Personal_Organization_For_New_User()
    {
        await _service.EnsureUserAsync("org@example.com");

        Assert.That(_dbContext.Set<Organization>().Any(), Is.False);
    }

    [Test]
    public async Task EnsureUserAsync_Does_Not_Create_Membership_For_New_User()
    {
        await _service.EnsureUserAsync("organizer@example.com");

        Assert.That(_dbContext.Set<Membership>().Any(), Is.False);
    }

    // ── EnsureUserAsync – returning user ──────────────────────────────────────

    [Test]
    public async Task EnsureUserAsync_Returns_Same_Ids_On_Second_Call_For_Same_Email()
    {
        var (userId1, workProfileId1) = await _service.EnsureUserAsync("returning@example.com");
        var (userId2, workProfileId2) = await _service.EnsureUserAsync("returning@example.com");

        Assert.That(userId2, Is.EqualTo(userId1));
        Assert.That(workProfileId2, Is.EqualTo(workProfileId1));
        Assert.That(workProfileId2, Is.Null);
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
    public async Task EnsureUserAsync_Does_Not_Create_WorkProfiles_On_Repeated_Login()
    {
        await _service.EnsureUserAsync("dup-wp@example.com");
        await _service.EnsureUserAsync("dup-wp@example.com");

        var userId = _dbContext.Set<User>().First(u => u.Email == "dup-wp@example.com").Id;
        var profileCount = _dbContext.Set<WorkProfile>()
            .Include(wp => wp.Membership)
            .Count(wp => wp.Membership.UserId == userId);

        Assert.That(profileCount, Is.EqualTo(0));
    }

    // ── EnsureUserAsync – different users are independent ────────────────────

    [Test]
    public async Task EnsureUserAsync_Does_Not_Create_WorkProfiles_For_Different_Emails()
    {
        var (_, profileA) = await _service.EnsureUserAsync("a@example.com");
        var (_, profileB) = await _service.EnsureUserAsync("b@example.com");

        Assert.Multiple(() =>
        {
            Assert.That(profileA, Is.Null);
            Assert.That(profileB, Is.Null);
            Assert.That(_dbContext.Set<WorkProfile>().Any(), Is.False);
        });
    }

    [Test]
    public async Task EnsureUserAsync_Reuses_User_When_Auth_Subject_Matches_After_Email_Change()
    {
        var (firstUserId, firstProfileId) = await _service.EnsureUserAsync(
            "before@example.com",
            "auth0|abc");

        var (secondUserId, secondProfileId) = await _service.EnsureUserAsync(
            "after@example.com",
            "auth0|abc");

        var user = await _dbContext.Users.FindAsync(secondUserId);

        Assert.Multiple(() =>
        {
            Assert.That(secondUserId, Is.EqualTo(firstUserId));
            Assert.That(secondProfileId, Is.EqualTo(firstProfileId));
            Assert.That(user!.Email, Is.EqualTo("after@example.com"));
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
            Assert.That(updated.Email, Is.EqualTo("updated@example.com"));
            Assert.That(updated.Timezone, Is.EqualTo("Europe/Paris"));
            Assert.That(user!.EditedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task UpdateProfileAsync_Rejects_Empty_Display_Name()
    {
        var (userId, _) = await _service.EnsureUserAsync("empty-name@example.com");

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.UpdateProfileAsync(
                userId,
                new UpdateUserProfileCommand("", "valid@example.com", null, "Europe/Berlin")));
    }

    [Test]
    public async Task UpdateProfileAsync_Rejects_Invalid_Email()
    {
        var (userId, _) = await _service.EnsureUserAsync("invalid-email-start@example.com");

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.UpdateProfileAsync(
                userId,
                new UpdateUserProfileCommand("Valid Name", "invalid-email", null, "Europe/Berlin")));
    }
}