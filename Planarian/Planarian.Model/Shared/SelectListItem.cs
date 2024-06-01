namespace Planarian.Model.Shared;

public class SelectListItem<TValue>
{
    public SelectListItem(string display, TValue value)
    {
        Display = display;
        Value = value;
    }

    public SelectListItem()
    {
    }

    public string Display { get; set; } = null!;
    public TValue Value { get; set; } = default!;
}