namespace WorshipSearchWeb.Models;

public class AddRestrictionRequest
{
    public string Term { get; set; } = string.Empty;
}

public class LogSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int ResultCount { get; set; }
}

public class LogSelectionRequest
{
    public string Query { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public bool PickedManually { get; set; }
}
