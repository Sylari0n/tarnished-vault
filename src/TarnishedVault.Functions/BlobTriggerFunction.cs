using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TarnishedVault.Functions;

public class BlobTriggerFunction
{
    private readonly ILogger<BlobTriggerFunction> _logger;

    public BlobTriggerFunction(ILogger<BlobTriggerFunction> logger) => _logger = logger;

    [Function("ScreenshotUploadLogger")]
    public void Run(
        [BlobTrigger("screenshots/{name}", Connection = "AzureWebJobsStorage")] Stream stream,
        string name)
    {
        _logger.LogInformation(
            "[Tarnished Vault] Screenshot uploaded — Name: {Name}, Size: {Size} bytes, Timestamp: {Time}",
            name, stream.Length, DateTime.UtcNow);
    }
}
