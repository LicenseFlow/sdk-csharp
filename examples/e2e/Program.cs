using System;
using System.Threading.Tasks;
using LicenseFlow;

static string MustEnv(string k) {
    var v = Environment.GetEnvironmentVariable(k);
    if (string.IsNullOrEmpty(v)) { Console.Error.WriteLine($"Missing {k}"); Environment.Exit(2); }
    return v!;
}

var c  = new LicenseFlowClient(MustEnv("LICENSEFLOW_API_URL"), MustEnv("LICENSEFLOW_API_KEY"));
var lk = MustEnv("LICENSE_KEY");
var rk = MustEnv("REVOKED_LICENSE_KEY");

await c.ActivateAsync(lk, "ci-csharp");
if (!(await c.VerifyAsync(lk)).Valid) throw new Exception("active must verify");
if ((await c.VerifyAsync(rk)).Valid)  throw new Exception("revoked must not verify");
await c.DeactivateAsync(lk);
Console.WriteLine("C# SDK E2E ✓");