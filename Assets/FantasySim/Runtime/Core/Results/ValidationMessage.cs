namespace FantasySim.Core.Results
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly record struct ValidationMessage(
        ValidationSeverity Severity,
        string Code,
        string Message
    );
}
