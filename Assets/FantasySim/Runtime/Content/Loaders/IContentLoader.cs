using System.Collections.Generic;
using FantasySim.Core.Results;

namespace FantasySim.Content.Loaders
{
    public interface IContentLoader<TDefinition>
    {
        IReadOnlyList<TDefinition> Load();
        IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<TDefinition> definitions);
    }
}
