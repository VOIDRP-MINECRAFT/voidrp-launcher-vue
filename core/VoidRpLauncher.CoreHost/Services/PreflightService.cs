using System;
using System.Collections.Generic;
using System.IO;
using VoidRpLauncher.CoreHost.Contracts;
using VoidRpLauncher.CoreHost.Models;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Cheap checks the renderer runs right before "Play", so a player hears about a setup that
/// is likely to crash (too little memory, a full disk, an unresolved crash) before waiting
/// through a multi-minute modded startup — instead of after.
/// </summary>
public sealed class PreflightService
{
    private const long ReservedForOsMb = 2048;
    private const long MinFreeDiskMb = 3072;

    private readonly LauncherSettingsService _settingsService;
    private readonly LauncherPathsService _pathsService;
    private readonly CrashRuleService _crashRules;
    private readonly CrashHistoryService _crashHistory;
    private readonly DiagnosticsService _diagnostics;

    public PreflightService(LauncherSettingsService settingsService, LauncherPathsService pathsService,
        CrashRuleService crashRules, CrashHistoryService crashHistory, DiagnosticsService diagnostics)
    {
        _settingsService = settingsService;
        _pathsService = pathsService;
        _crashRules = crashRules;
        _crashHistory = crashHistory;
        _diagnostics = diagnostics;
    }

    public PreflightResultDto Run()
    {
        var result = new PreflightResultDto();
        try { CheckMemory(result.Warnings); } catch (Exception ex) { _diagnostics.Warn("Preflight", $"Memory check failed: {ex.Message}"); }
        try { CheckDisk(result.Warnings); } catch (Exception ex) { _diagnostics.Warn("Preflight", $"Disk check failed: {ex.Message}"); }
        try { CheckLastCrash(result.Warnings); } catch (Exception ex) { _diagnostics.Warn("Preflight", $"Crash check failed: {ex.Message}"); }
        return result;
    }

    private void CheckMemory(List<PreflightWarningDto> warnings)
    {
        var allocatedMb = _settingsService.Load().MaxRamMb;
        var recommendedMb = _crashRules.RecommendedRamMb();
        long totalMb = 0;
        try { totalMb = LauncherSettingsService.GetTotalPhysicalMemoryBytes() / (1024 * 1024); } catch { }

        if (totalMb > 0 && allocatedMb > totalMb - ReservedForOsMb)
        {
            warnings.Add(new PreflightWarningDto
            {
                Id = "ram_above_system",
                Severity = "critical",
                Title = "Игре выделено слишком много памяти",
                Message = $"Выделено {Gb(allocatedMb)}, а на компьютере всего {Gb(totalMb)}. Системе не останется памяти, и игра может вылететь или зависнуть весь компьютер. Выделите не больше {Gb(Math.Max(2048, totalMb - ReservedForOsMb))}.",
                Actions = { Action(0, CrashActionTypes.OpenSettings, "Настроить память") },
            });
            return;
        }

        if (allocatedMb < recommendedMb)
        {
            var canRaise = totalMb == 0 || totalMb - ReservedForOsMb >= recommendedMb;
            warnings.Add(new PreflightWarningDto
            {
                Id = "ram_below_recommended",
                Severity = "warning",
                Title = "Игре выделено мало памяти",
                Message = canRaise
                    ? $"Выделено {Gb(allocatedMb)}, а сборке нужно минимум {Gb(recommendedMb)}. С таким объёмом игра часто падает при загрузке или на входе на сервер."
                    : $"Выделено {Gb(allocatedMb)}, сборке нужно {Gb(recommendedMb)}, а на компьютере всего {Gb(totalMb)}. Выделите максимум, который можно, и закройте браузер и другие программы перед запуском.",
                Actions = { Action(0, CrashActionTypes.OpenSettings, "Настроить память") },
            });
        }
    }

    private void CheckDisk(List<PreflightWarningDto> warnings)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(_pathsService.GameDirectory));
        if (string.IsNullOrWhiteSpace(root)) return;
        var freeMb = new DriveInfo(root).AvailableFreeSpace / (1024 * 1024);
        if (freeMb >= MinFreeDiskMb) return;

        warnings.Add(new PreflightWarningDto
        {
            Id = "disk_low",
            Severity = "warning",
            Title = "На диске почти не осталось места",
            Message = $"На диске {root} свободно {Gb(freeMb)}. Обновление сборки может не скачаться, а игра — не сохранить настройки. Освободите хотя бы 3 ГБ.",
        });
    }

    private void CheckLastCrash(List<PreflightWarningDto> warnings)
    {
        var latest = _crashHistory.Latest();
        if (latest is null) return;

        var warning = new PreflightWarningDto
        {
            Id = "last_crash",
            Severity = "info",
            Title = "В прошлый раз игра завершилась с ошибкой",
            Message = $"«{latest.Title}». Если вы ещё не применили исправление — откройте подсказку, там есть кнопка, которая это чинит.",
        };
        if (latest.Advice is not null)
            warning.Actions.Add(Action(0, CrashActionTypes.ShowCrash, "Открыть подсказку"));
        warnings.Add(warning);
    }

    private static CrashActionDto Action(int index, string type, string label) => new() { Index = index, Type = type, Label = label };

    private static string Gb(long mb) => $"{mb / 1024.0:0.#} ГБ";
}
