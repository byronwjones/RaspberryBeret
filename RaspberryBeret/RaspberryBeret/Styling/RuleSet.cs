using System.Collections.Generic;
using System.Linq;

namespace RaspberryBeret.Styling;
internal class RuleSet
{
    public ElementSelector Selector { get; set; } = new ElementSelector([]);
    public IEnumerable<Style> Styles { get; set; } = Enumerable.Empty<Style>();
}
