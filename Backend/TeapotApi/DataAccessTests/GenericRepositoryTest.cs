using DataAccess.Models;
using DataAccess.Repositories;

namespace DataAccessTests;

[Category("Integration")]
public class GenericRepositoryTest
{
    private TeapotDbContext _dbContext;
    private GenericRepository<Invitation> _repository;

    [SetUp]
    public void Setup()
    {
        _dbContext = new TeapotDbContext();
        _repository = new GenericRepository<Invitation>(_dbContext);
    }

    [Test]
    public async Task GetManyTest()
    {
        var repos = await _repository.GetManyAsync(u => u.Status == EInvitationStatus.Open);
        Assert.That(repos, Is.Not.Null);
    }
    
    [Test]

    [TearDown]
    public void Teardown()
    {
        _dbContext.Dispose();
        _repository.Dispose();
    }
}