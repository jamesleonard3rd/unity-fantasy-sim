namespace FantasySim.Core.Results
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly struct ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        public ValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
    }
}
