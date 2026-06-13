using FantasySim.Content.Validation;

namespace FantasySim.Content.Catalog
{
    public sealed class ContentCatalogBuildResult
    {
        public ContentCatalogBuildResult(ContentCatalog catalog, ContentValidationResult validationResult)
        {
            Catalog = catalog;
            ValidationResult = validationResult;
        }

        public ContentCatalog Catalog { get; }
        public ContentValidationResult ValidationResult { get; }
        public bool IsValid => ValidationResult.IsValid;
    }
}
