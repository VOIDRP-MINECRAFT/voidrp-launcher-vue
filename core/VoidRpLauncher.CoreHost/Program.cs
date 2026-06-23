using Microsoft.AspNetCore.Http.Json;
using VoidRpLauncher.CoreHost.Configuration;
using VoidRpLauncher.CoreHost.Contracts;
using VoidRpLauncher.CoreHost.Models;
using VoidRpLauncher.CoreHost.Services;
using VoidRpLauncher.CoreHost.Services.Account;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
});

var endpoints = new AppEndpointsOptions();
builder.Configuration.GetSection("Launcher").Bind(endpoints);
if (string.IsNullOrWhiteSpace(endpoints.AccountApiBaseUrl))
{
    endpoints.AccountApiBaseUrl = "https://api.void-rp.ru/api/v1";
    endpoints.PackManifestUrl = "https://void-rp.ru/launcher/manifests/manifest.json";
    endpoints.RuntimeSeedUrl = "https://void-rp.ru/launcher/runtime-seed";
    endpoints.RuntimeManifestBaseUrl = "https://void-rp.ru/launcher/manifests";
    endpoints.RegisterUrl = "https://void-rp.ru/register";
    endpoints.ForgotPasswordUrl = "https://void-rp.ru/forgot-password";
    endpoints.VerifyEmailUrl = "https://void-rp.ru/verify-email";
}

builder.Services.AddSingleton(endpoints);
builder.Services.AddSingleton<DiagnosticsService>();
builder.Services.AddSingleton<AppVersionService>();
builder.Services.AddSingleton<LauncherPlatformService>();
builder.Services.AddSingleton<LauncherPathsService>();
builder.Services.AddSingleton<HashService>();
builder.Services.AddSingleton<LauncherSettingsService>();
builder.Services.AddSingleton<ManifestService>();
builder.Services.AddSingleton<FileSyncService>();
builder.Services.AddSingleton<ClientRepairService>();
builder.Services.AddSingleton<RuntimeBootstrapService>();
builder.Services.AddSingleton<LocalMinecraftLaunchService>();
builder.Services.AddSingleton<LauncherStateService>();

builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<AppEndpointsOptions>();
    return new LauncherAccountApiClient(new HttpClient(), opts.AccountApiBaseUrl);
});

builder.Services.AddSingleton(sp => new LauncherTokenStore(sp.GetRequiredService<LauncherPathsService>().StateDirectory));
builder.Services.AddSingleton(sp => new LauncherPlayTicketStore(sp.GetRequiredService<LauncherPathsService>().StateDirectory));
builder.Services.AddSingleton<LauncherAuthSessionService>();
builder.Services.AddSingleton<AuthenticatedLaunchService>();
builder.Services.AddSingleton<LauncherFacadeService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});

var app = builder.Build();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapGet("/api/bootstrap", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.InitializeAsync(CancellationToken.None)));

app.MapGet("/api/state", (LauncherFacadeService facade) =>
    Results.Ok(facade.GetState()));

app.MapPost("/api/auth/login", async (LoginCommandDto dto, LauncherFacadeService facade) =>
    Results.Ok(await facade.LoginAsync(dto.Login, dto.Password, CancellationToken.None)));

app.MapPost("/api/auth/logout", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.LogoutAsync(CancellationToken.None)));

app.MapPost("/api/auth/revoke-other-sessions", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.RevokeOtherSessionsAsync(CancellationToken.None)));

app.MapPost("/api/actions/play", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.PlayAsync(CancellationToken.None)));

app.MapPost("/api/actions/repair", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.RepairAsync(CancellationToken.None)));

app.MapGet("/api/settings", (LauncherFacadeService facade) =>
    Results.Ok(facade.GetState()));

app.MapPost("/api/settings", (SettingsUpdateDto dto, LauncherFacadeService facade) =>
    Results.Ok(facade.SaveSettings(dto.MaxRamMb)));

app.MapPost("/api/settings/reset", (LauncherFacadeService facade) =>
    Results.Ok(facade.ResetSettings()));

app.MapPost("/api/diagnostics/clear", (LauncherFacadeService facade) =>
    Results.Ok(facade.ClearDiagnostics()));

app.MapGet("/api/mods", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.GetModsAsync()));

app.MapPost("/api/mods/toggle", async (ModToggleDto dto, LauncherFacadeService facade) =>
    Results.Ok(await facade.ToggleModAsync(dto.Path, dto.Enabled)));

app.MapGet("/api/skin", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.GetSkinAsync(CancellationToken.None)));

app.MapPost("/api/skin", async (HttpRequest request, LauncherFacadeService facade) =>
{
    var form = await request.ReadFormAsync(CancellationToken.None);
    var file = form.Files["file"];
    var modelVariant = form.TryGetValue("modelVariant", out var rawModelVariant)
        ? rawModelVariant.ToString()
        : form.TryGetValue("model_variant", out var rawAltModelVariant)
            ? rawAltModelVariant.ToString()
            : "classic";

    if (file is null)
    {
        return Results.Ok(LauncherPlayerSkinOperationDto.Failure("Файл скина не передан."));
    }

    return Results.Ok(await facade.UploadSkinAsync(file, modelVariant, CancellationToken.None));
});

app.MapDelete("/api/skin", async (LauncherFacadeService facade) =>
    Results.Ok(await facade.DeleteSkinAsync(CancellationToken.None)));

app.MapPost("/api/mod-suggestions", async (ModSuggestionCommandDto dto, LauncherAuthSessionService auth) =>
{
    try
    {
        var result = await auth.SuggestModAsync(dto.Url, dto.Comment, CancellationToken.None);
        return Results.Ok(new ModSuggestionResponseDto { Ok = true, Message = result.Message });
    }
    catch (Exception ex)
    {
        return Results.Ok(new ModSuggestionResponseDto { Ok = false, Message = ex.Message });
    }
});

app.MapPost("/api/player-feedback", async (PlayerFeedbackCommandDto dto, LauncherAuthSessionService auth) =>
{
    try
    {
        var result = await auth.SubmitFeedbackAsync(dto.Type, dto.Title, dto.Body, CancellationToken.None);
        return Results.Ok(new PlayerFeedbackResponseDto { Ok = true, Message = result.Message });
    }
    catch (Exception ex)
    {
        return Results.Ok(new PlayerFeedbackResponseDto { Ok = false, Message = ex.Message });
    }
});

app.Run();
