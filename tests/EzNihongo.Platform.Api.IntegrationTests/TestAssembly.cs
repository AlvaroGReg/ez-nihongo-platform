using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

internal static class TestHost
{
    public static HttpClient CreateClient(Func<HttpClient> createClient, int authPermitLimit)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt__SigningKey"] = "integration-test-signing-key-with-more-than-32-bytes",
            ["Jwt__Issuer"] = "ez-nihongo-integration-tests",
            ["Jwt__Audience"] = "ez-nihongo-integration-tests",
            ["AuthRateLimit__PermitLimit"] = authPermitLimit.ToString(),
            ["AuthRateLimit__WindowMinutes"] = "1"
        };
        var previousValues = values.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);

        try
        {
            foreach (var (key, value) in values)
                Environment.SetEnvironmentVariable(key, value);

            return createClient();
        }
        finally
        {
            foreach (var (key, value) in previousValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
