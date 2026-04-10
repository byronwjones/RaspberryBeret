namespace RaspberryBeret.DataBinding;
public class BindingModel
{
    public BindingModel()
    {
        ContextTree = new Dictionary<string, object>();
        Index = -1;
    }

    /// <summary>
    /// Gets the context tree - a dictionary of every context in this model
    /// </summary>
    public Dictionary<string, object> ContextTree { get; private set; }

    /// <summary>
    /// Gets or sets the name of the current context
    /// </summary>
    public string NameOfCurrentContext { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current context
    /// </summary>
    public object CurrentContext { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection the current context is a member of
    /// </summary>
    public object? SourceCollection { get; set; }

    /// <summary>
    /// Gets or sets the index in the source collection of the element's context
    /// </summary>
    public int Index { get; set; }
}
