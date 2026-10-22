namespace CarService.Core.Common;

// WHAT: One page of results plus the numbers needed to draw a pager.
// WHY:  Never load a whole table to show 10 rows. The database counts and slices; the app only
//       ever holds one page in memory. This record is the shape every paged list returns.
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
