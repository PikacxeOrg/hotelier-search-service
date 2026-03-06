namespace SearchService.Api;

public class SearchPagedResponse
{
    public List<SearchResponse> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public long TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
