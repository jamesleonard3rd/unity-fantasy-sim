using System.Collections.Generic;
using System.Text;
using FantasySim.Core.Results;

namespace FantasySim.Content.Catalog
{
    public static class ContentCatalogValidationReport
    {
        public static string FormatSummary(ContentCatalogBuildResult result)
        {
            ContentCatalog catalog = result.Catalog;
            IReadOnlyList<ValidationMessage> messages = result.ValidationResult.Messages;

            var builder = new StringBuilder();
            builder.AppendLine(result.IsValid
                ? "Fantasy Sim game data is valid."
                : "Fantasy Sim game data has validation errors.");
            builder.AppendLine("Catalog counts:");
            builder.AppendLine($"- Factions: {catalog.Factions.Count}");
            builder.AppendLine($"- Races: {catalog.Races.Count}");
            builder.AppendLine($"- Stats: {catalog.Stats.Count}");
            builder.AppendLine($"- Traits: {catalog.Traits.Count}");
            builder.AppendLine($"- Regions: {catalog.Regions.Count}");
            builder.AppendLine($"- Entity templates: {catalog.EntityTemplates.Count}");
            builder.AppendLine($"Validation messages: {messages.Count}");

            return builder.ToString().TrimEnd();
        }

        public static string FormatMessage(ValidationMessage message)
        {
            return $"[{message.Severity}] {message.Code}: {message.Message}";
        }
    }
}
