namespace WorshipSearchWeb.Models;

public class SearchQuery
{
    public string Query { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    // null = all, "approved", "review"
    public string? StatusFilter { get; set; } = null;
}
