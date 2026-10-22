using CarService.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace CarService.Infrastructure.Data;

public static class QueryableExtensions
{
    // WHAT: An extension method - `query.ToPagedResultAsync(...)` reads like it belongs to IQueryable.
    // NOTE: `query` is still an unexecuted description of SQL at this point. Count and Skip/Take
    //       are added to it, so the database does the counting and slicing (two queries total).
    // IMPORTANT: the caller must give the query a deterministic ORDER BY. Without one, rows may
    //       shuffle between pages and you'd see duplicates or miss rows.
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync();

        // Clamp instead of failing: ?page=999 or ?page=-3 just shows the nearest real page.
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<T>(items, page, pageSize, total);
    }
}
