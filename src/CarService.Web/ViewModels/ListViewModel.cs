using CarService.Core.Common;
using CarService.Core.Entities;

namespace CarService.Web.ViewModels;

// WHAT: What a searchable, pageable list page needs: the page of rows PLUS the filters that
//       produced it (so the search box can show its current value and the pager can keep it).
public class ListViewModel<T>
{
    public required PagedResult<T> Result { get; init; }
    public string? Search { get; init; }
    public AppointmentStatus? Status { get; init; }

    public PagerViewModel Pager
    {
        get
        {
            // Only filters that are actually set go into the page links, so URLs stay clean:
            // /Vehicles?search=golf&page=2   instead of   /Vehicles?search=golf&status=&page=2
            var route = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Search)) route["search"] = Search;
            if (Status is not null) route["status"] = Status.ToString()!;
            return new PagerViewModel(Result.Page, Result.TotalPages, Result.TotalCount, route);
        }
    }
}

public record PagerViewModel(int Page, int TotalPages, int TotalCount, IReadOnlyDictionary<string, string> Route)
{
    // Route values for the link to another page = the current filters + that page number.
    public IDictionary<string, string> RouteFor(int page) =>
        new Dictionary<string, string>(Route) { ["page"] = page.ToString() };
}
