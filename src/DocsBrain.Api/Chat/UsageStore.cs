using DocsBrain.Api.Data;
using DocsBrain.Core.Chat;
using Microsoft.EntityFrameworkCore;

namespace DocsBrain.Api.Chat;

public sealed class UsageStore(DocsBrainDbContext db, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<long> GetTodayAsync(CancellationToken ct)
    {
        var day = Today;
        return await db.Usage.Where(u => u.Day == day).Select(u => u.Tokens).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(long tokens, CancellationToken ct)
    {
        var day = Today;
        for (var attempt = 0; ; attempt++)
        {
            var row = await db.Usage.FirstOrDefaultAsync(u => u.Day == day, ct);
            if (row is null) db.Usage.Add(new UsageRecord { Day = day, Tokens = tokens, Requests = 1 });
            else { row.Tokens += tokens; row.Requests++; }

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Two first requests of the day raced on the unique (tenant, day) row: reload and add to it.
                db.ChangeTracker.Clear();
            }
        }
    }
}
