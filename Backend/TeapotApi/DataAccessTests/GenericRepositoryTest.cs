using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DataAccessTests;

[Category("Integration")]
public class GenericRepositoryTest
{
    private TeapotDbContext _dbContext;
    private GenericRepository<Invitation> _repository;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TeapotDbContext(options);
        _repository = new GenericRepository<Invitation>(_dbContext);
    }

    [Test]
    public async Task GetManyTest()
    {
        var repos = await _repository.GetManyAsync(u => u.Status == EInvitationStatus.Open);
        Assert.That(repos, Is.Not.Null);
    }

    [TearDown]
    public void Teardown()
    {
        _dbContext.Dispose();
        _repository.Dispose();
    }
}