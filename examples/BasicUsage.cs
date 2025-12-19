using LicenseFlow.SDK;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new LicenseFlowClient(
            "https://api.test",
            "test-api-key"
        );

        Console.WriteLine("--- LicenseFlow C# Example ---");

        try
        {
            // 1. Activate
            Console.WriteLine("Activating license...");
            var activation = await client.ActivateAsync("DEMO-KEY", "DotNet-Server");
            Console.WriteLine($"Result: {activation}");

            // 2. Verify
            Console.WriteLine("Verifying license...");
            var verification = await client.VerifyAsync("DEMO-KEY");
            Console.WriteLine($"Is Valid: {verification.valid}");

            // 3. Deactivate
            Console.WriteLine("Deactivating license...");
            var deactivation = await client.DeactivateAsync("DEMO-KEY");
            Console.WriteLine($"Result: {deactivation}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
