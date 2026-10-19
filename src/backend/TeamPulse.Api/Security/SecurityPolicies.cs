namespace TeamPulse.Api.Security;

public static class SecurityPolicies
{
    public const string FrontendCors = "frontend";
    public const string AuthRateLimit = "auth-rate-limit";
    public const string ImportRateLimit = "import-rate-limit";
    public const string CorrelationHeader = "X-Correlation-ID";
}
