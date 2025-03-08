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

public class SelectListItem<TValue, TData> : SelectListItem<TValue>
{
    public SelectListItem(string display, TValue value, TData data) : base(display, value)
    {
        Data = data;
    }

    public SelectListItem()
    {
    }

    public TData Data { get; set; } = default!;
}