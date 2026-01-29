using PadelApi.DTOs.Requests;
using PadelApi.DTOs.Responses;
using PadelApi.Exceptions;
using PadelApi.Repositories;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Text.Json;
using PadelApi.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IMatchService, MatchService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok("OK"));
app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapPost("/users/{userId}", async (string userId, UpsertUserRequest request, IUserRepository userRepo) =>
{
    try
    {
        var user = await userRepo.UpsertAsync(userId, request.Name, request.Email);
        return Results.Ok(new UserResponse(user.Id, user.Name, user.Email));
    }
    catch (ConflictException ex)
    {
        return Results.Conflict(new ErrorResponse(ex.Message));
    }
})
.WithName("UpsertUser");

app.MapPost("/matches", async (CreateMatchRequest request, IMatchService matchService) =>
{
    var response = await matchService.CreateMatchAsync(request);

    var existingMatch = await matchService.GetActiveMatchAsync(request.UserId);
    if (existingMatch.MatchId != response.MatchId)
    {
        return Results.Ok(existingMatch);
    }

    return Results.Created($"/matches/{response.MatchId}", response);
})
.WithName("CreateMatch");

app.MapGet("/matches/active", async (string userId, IMatchService matchService) =>
{
    try
    {
        var response = await matchService.GetActiveMatchAsync(userId);
        return Results.Ok(response);
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
})
.WithName("GetActiveMatch");

app.MapPut("/matches/{matchId:guid}/point", async (Guid matchId, HttpRequest httpRequest, IMatchService matchService) =>
{
    try
    {
        var json = await new StreamReader(httpRequest.Body).ReadToEndAsync();
        Console.WriteLine($"DEBUG JSON: {json}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = JsonSerializer.Deserialize<RegisterPointRequest>(json, options);

        if (request == null) return Results.BadRequest("Null request");

        var response = await matchService.RegisterPointAsync(matchId, request);
        return Results.Ok(response);
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"JSON ERROR: {ex.Message}");
        return Results.BadRequest(new ErrorResponse($"JSON Error: {ex.Message}"));
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (ForbiddenException ex)
    {
        return Results.Json(new ErrorResponse(ex.Message), statusCode: 403);
    }
    catch (ConflictException ex)
    {
        return Results.Conflict(new ErrorResponse(ex.Message, ex.Details));
    }
})
.WithName("RegisterPoint");

app.MapPost("/matches/{matchId:guid}/undo", async (Guid matchId, UndoRequest request, IMatchService matchService) =>
{
    try
    {
        var response = await matchService.UndoAsync(matchId, request);
        return Results.Ok(response);
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (ForbiddenException ex)
    {
        return Results.Json(new ErrorResponse(ex.Message), statusCode: 403);
    }
    catch (ConflictException ex)
    {
        return Results.Conflict(new ErrorResponse(ex.Message, ex.Details));
    }
})
.WithName("Undo");

app.MapPut("/matches/{matchId:guid}/state", async (Guid matchId, UpdateStateRequest request, IMatchService matchService) =>
{
    try
    {
        var response = await matchService.UpdateStateAsync(matchId, request);
        return Results.Ok(response);
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (ForbiddenException ex)
    {
        return Results.Json(new ErrorResponse(ex.Message), statusCode: 403);
    }
    catch (ConflictException ex)
    {
        return Results.Conflict(new ErrorResponse(ex.Message, ex.Details));
    }
})
.WithName("UpdateState");

app.MapPost("/matches/{matchId:guid}/finish", async (Guid matchId, FinishMatchRequest request, IMatchService matchService) =>
{
    try
    {
        await matchService.FinishMatchAsync(matchId, request);
        return Results.Ok(new { message = "Match finished successfully" });
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (ForbiddenException ex)
    {
        return Results.Json(new ErrorResponse(ex.Message), statusCode: 403);
    }
    catch (ConflictException ex)
    {
        return Results.Conflict(new ErrorResponse(ex.Message));
    }
})
.WithName("FinishMatch");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var env = app.Environment;
var config = app.Configuration;

logger.LogInformation("========================================");
logger.LogInformation("🏓 Padel Score API Starting...");
logger.LogInformation("Environment: {Environment}", env.EnvironmentName);
logger.LogInformation("Connection: {Connection}",
    config.GetConnectionString("Default")?.Split(';')[0] ?? "Not configured");
logger.LogInformation("========================================");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
    logger.LogInformation("========================================");
    logger.LogInformation("✅ Padel Score API is running!");
    if (addresses != null)
    {
        foreach (var address in addresses)
        {
            logger.LogInformation("🌐 Listening on: {Address}", address);
        }
    }
    logger.LogInformation("📖 Swagger UI: {SwaggerUrl}", addresses?.FirstOrDefault()?.Replace("[::]:", "localhost:") + "/swagger");
    logger.LogInformation("========================================");
});

app.Run();
