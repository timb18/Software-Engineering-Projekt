using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class WorkProfileRepository(TeapotDbContext context): GenericRepository<WorkProfile>(context), IWorkProfileRepository
{
    private readonly DbSet<WorkProfile> _dbSet = context.Set<WorkProfile>();
    public async Task<WorkProfile> GetWorkProfileWithWorkDayProfileByIdAsync(Guid workProfileId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Include(wp => wp.WorkDayProfiles)
            .ThenInclude(wdp => wdp.WorkBlocks).Include(wb => wb.WorkDayProfiles)
            .ThenInclude(wbp => wbp.WorkBreaks)
            .Where(wp => wp.Id == workProfileId).FirstOrDefaultAsync(cancellationToken);
    }
}