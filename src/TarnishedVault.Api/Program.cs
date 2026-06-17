using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using TarnishedVault.Api.Models;
using TarnishedVault.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

var cosmosConnectionString = builder.Configuration["CosmosDb:ConnectionString"]
    ?? throw new InvalidOperationException("CosmosDb:ConnectionString is not configured");
builder.Services.AddSingleton(new CosmosClient(
    cosmosConnectionString,
    new CosmosClientOptions { Serializer = new CosmosSystemTextJsonSerializer(jsonOptions) }
));

var blobConnectionString = builder.Configuration["BlobStorage:ConnectionString"]
    ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not configured");
builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));

var app = builder.Build();

var cosmosClient = app.Services.GetRequiredService<CosmosClient>();
var dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync("TarnishedVaultDB");
await dbResponse.Database.CreateContainerIfNotExistsAsync("Builds", "/game");

var blobServiceClient = app.Services.GetRequiredService<BlobServiceClient>();
var screenshotsContainer = blobServiceClient.GetBlobContainerClient("screenshots");
await screenshotsContainer.CreateIfNotExistsAsync(PublicAccessType.Blob);

string[] validGames    = ["warframe", "helldivers2", "eldenring"];
string[] validSections = ["build", "fashion"];

app.MapPost("/builds/{game}/{section}", async (
    string game, string section, HttpRequest request,
    CosmosClient cosmos, BlobServiceClient blobClient) =>
{
    game    = game.ToLower();
    section = section.ToLower();

    if (!validGames.Contains(game))
        return Results.BadRequest($"Invalid game. Valid values: {string.Join(", ", validGames)}");
    if (!validSections.Contains(section))
        return Results.BadRequest($"Invalid section. Valid values: {string.Join(", ", validSections)}");

    var form = await request.ReadFormAsync();
    var dataJson = form["data"].ToString();
    if (string.IsNullOrWhiteSpace(dataJson))
        return Results.BadRequest("Missing 'data' form field.");

    Dictionary<string, JsonElement> data;
    try { data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson, jsonOptions) ?? new(); }
    catch { return Results.BadRequest("Invalid JSON in 'data' field."); }

    var author = data.TryGetValue("author", out var authorEl)
        ? authorEl.GetString() ?? "Anonymous"
        : "Anonymous";

    var build = new Build { Game = game, Section = section, Author = author, Data = data };

    if (section == "fashion" && form.Files.Count > 0)
    {
        var file     = form.Files[0];
        var blobName = $"{build.Id}-{Path.GetFileName(file.FileName)}";
        var blob     = blobClient.GetBlobContainerClient("screenshots").GetBlobClient(blobName);
        await blob.UploadAsync(file.OpenReadStream(), new BlobHttpHeaders { ContentType = file.ContentType });
        build.ScreenshotUrl = blob.Uri.ToString();
    }

    var container = cosmos.GetContainer("TarnishedVaultDB", "Builds");
    var response  = await container.CreateItemAsync(build, new PartitionKey(build.Game));
    return Results.Created($"/builds/{game}/{section}/{response.Resource.Id}", response.Resource);
});

app.MapGet("/builds/{game}", async (string game, CosmosClient cosmos) =>
{
    var query = new QueryDefinition("SELECT * FROM c WHERE c.game = @game")
        .WithParameter("@game", game.ToLower());
    return Results.Ok(await FetchBuilds(cosmos, query));
});

app.MapGet("/builds/{game}/{section}", async (string game, string section, CosmosClient cosmos) =>
{
    var query = new QueryDefinition("SELECT * FROM c WHERE c.game = @game AND c.section = @section")
        .WithParameter("@game", game.ToLower())
        .WithParameter("@section", section.ToLower());
    return Results.Ok(await FetchBuilds(cosmos, query));
});

app.Run();

static async Task<List<Build>> FetchBuilds(CosmosClient cosmos, QueryDefinition query)
{
    var container = cosmos.GetContainer("TarnishedVaultDB", "Builds");
    var iterator  = container.GetItemQueryIterator<Build>(query);
    var results   = new List<Build>();
    while (iterator.HasMoreResults)
    {
        var page = await iterator.ReadNextAsync();
        results.AddRange(page);
    }
    return results;
}
