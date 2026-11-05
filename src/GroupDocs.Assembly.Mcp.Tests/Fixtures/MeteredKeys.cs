namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

/// The metered key pair the suite forwards to the server under test.
internal static class MeteredKeys
{
    public const string PublicKeyVariable = "GROUPDOCS_METERED_PUBLIC_KEY";
    public const string PrivateKeyVariable = "GROUPDOCS_METERED_PRIVATE_KEY";

    public static string? PublicKey => Value(PublicKeyVariable);
    public static string? PrivateKey => Value(PrivateKeyVariable);

    private static string? Value(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static bool Configured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);

    /// <summary>Fails loudly when the metered key pair is absent.</summary>
    /// <remarks>
    /// Deliberately a hard failure rather than a skip. The external audit found
    /// license-dependent tests silently no-opping and being reported as Passed, which
    /// overstates coverage - the metered job is gated in CI, where the decision is visible,
    /// so by the time a test runs the keys must be there.
    /// </remarks>
    public static void RequireConfigured()
    {
        if (Configured) return;

        var missing = string.IsNullOrWhiteSpace(PublicKey)
            ? string.IsNullOrWhiteSpace(PrivateKey) ? "both keys" : PublicKeyVariable
            : PrivateKeyVariable;

        throw new InvalidOperationException(
            $"Metered tests require a real metered key pair, but {missing} is not set. " +
            $"Set {PublicKeyVariable} and {PrivateKeyVariable} (in CI these come from repository " +
            "or organization secrets). These tests are not skipped on purpose: a silent skip " +
            "reported as a pass is how untested licensing ships.");
    }
}
