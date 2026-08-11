using System;
using System.Collections.Generic;
using System.Linq;
using VoidRpLauncher.CoreHost.Contracts;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Turns a raw game crash (exit code + log tail + crash-report file) into a
/// player-friendly explanation with a concrete suggested fix. Purely heuristic
/// pattern-matching over the log text — the same signal an admin would eyeball,
/// surfaced to the player so they can self-serve instead of just seeing a code.
/// </summary>
public static class CrashAdvisor
{
    private sealed record Rule(
        string Title,
        string Cause,
        string Solution,
        Func<int, string, bool> Matches);

    // Ordered most-specific → most-generic. First match wins.
    private static readonly Rule[] Rules =
    {
        // Mod jars vanished / were locked mid-scan while NeoForge set up the game dir.
        // Classic Windows antivirus quarantine or a sync race on the mods folder.
        new(
            "Антивирус заблокировал файлы сборки",
            "Во время запуска часть модов в папке mods оказалась недоступна (файлы удалены или заблокированы). Обычно это антивирус (чаще всего Windows Defender), который держит или помещает .jar-моды в карантин.",
            "1) Добавьте папку лаунчера в исключения антивируса: %LOCALAPPDATA%\\VoidRpLauncher\n2) Нажмите «Переустановить» в лаунчере, чтобы докачать файлы заново.\n3) Запустите игру снова.",
            (exit, log) =>
                log.Contains("setup the game directory", StringComparison.OrdinalIgnoreCase)
                || (log.Contains("NoSuchFileException", StringComparison.OrdinalIgnoreCase)
                    && log.Contains("mods", StringComparison.OrdinalIgnoreCase))),

        // Out of memory — allocated heap too small for the modpack.
        new(
            "Не хватает оперативной памяти",
            "Игре не хватило выделенной оперативной памяти (OutOfMemoryError / heap space). Для этой сборки выделено слишком мало ОЗУ.",
            "Откройте «Настройки» в лаунчере и увеличьте выделяемую память (рекомендуется 6–8 ГБ). Если на ПК всего 8 ГБ ОЗУ — закройте браузер и другие программы перед запуском.",
            (exit, log) =>
                log.Contains("OutOfMemoryError", StringComparison.OrdinalIgnoreCase)
                || log.Contains("java heap space", StringComparison.OrdinalIgnoreCase)
                || log.Contains("GC overhead limit", StringComparison.OrdinalIgnoreCase)),

        // GPU / OpenGL driver can't provide the required GL version.
        new(
            "Проблема с драйвером видеокарты",
            "Не удалось инициализировать OpenGL нужной версии. Обычно причина — устаревший или отсутствующий драйвер видеокарты, либо запуск на встроенной графике.",
            "1) Обновите драйвер видеокарты (NVIDIA/AMD/Intel) до последней версии.\n2) На ноутбуке убедитесь, что игра запускается на дискретной видеокарте.\n3) Перезагрузите ПК и попробуйте снова.",
            (exit, log) =>
                log.Contains("Requested GL version", StringComparison.OrdinalIgnoreCase)
                && log.Contains("got version", StringComparison.OrdinalIgnoreCase)
                && log.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
                && !log.Contains("got version 4.6", StringComparison.OrdinalIgnoreCase)
                || log.Contains("Couldn't set pixel format", StringComparison.OrdinalIgnoreCase)
                || log.Contains("Failed to create window", StringComparison.OrdinalIgnoreCase)
                || log.Contains("EXCEPTION_ACCESS_VIOLATION", StringComparison.OrdinalIgnoreCase)
                   && (log.Contains("nvoglv", StringComparison.OrdinalIgnoreCase)
                       || log.Contains("atio6axx", StringComparison.OrdinalIgnoreCase)
                       || log.Contains("ig", StringComparison.OrdinalIgnoreCase)
                          && log.Contains("igdumd", StringComparison.OrdinalIgnoreCase))),

        // A specific mod threw during construction/loading.
        new(
            "Ошибка в одном из модов",
            "Один из модов сборки завершил игру с ошибкой при загрузке. Часто это следствие повреждённого файла мода или конфликта после незавершённого обновления.",
            "1) Нажмите «Переустановить» в лаунчере — это перекачает и проверит все файлы.\n2) Если не помогло — пришлите этот отчёт в поддержку, мы посмотрим по логу.",
            (exit, log) =>
                log.Contains("Failed to load mods", StringComparison.OrdinalIgnoreCase)
                || log.Contains("LoadingModList", StringComparison.OrdinalIgnoreCase)
                || log.Contains("net.neoforged.fml.ModLoadingException", StringComparison.OrdinalIgnoreCase)
                || log.Contains("Caught exception during event", StringComparison.OrdinalIgnoreCase)),

        // MCEF / CEF native crash (0xC0000409).
        new(
            "Сбой встроенного браузера (MCEF)",
            "Игра упала из-за встроенного браузера MCEF/CEF (код 0xC0000409). Обычно это разовый сбой или заблокированные антивирусом файлы CEF.",
            "1) Запустите игру ещё раз — часто помогает.\n2) Если повторяется — добавьте папку лаунчера в исключения антивируса и нажмите «Переустановить».",
            (exit, log) => exit == -1073740791),
    };

    /// <summary>
    /// Human-readable NTSTATUS/hex hints keyed by the unsigned hex form of the
    /// exit code. Kept in sync with the admin panel's EXIT_HINTS table.
    /// </summary>
    private static readonly Dictionary<string, string> ExitCodeHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0xC0000005"] = "Access Violation — нативный краш (драйвер/GPU/мод с native-библиотекой)",
        ["0xC0000409"] = "Stack buffer overrun (обычно MCEF/CEF)",
        ["0xC00000FD"] = "Переполнение стека (stack overflow)",
        ["0xE0434352"] = "Необработанное .NET-исключение",
        ["0xDEAD"] = "JVM Runtime.halt",
        ["0x1"] = "Общая ошибка запуска JVM (см. лог)",
    };

    public static string ExitCodeHex(int code)
    {
        var u = unchecked((uint)code);
        return "0x" + u.ToString("X");
    }

    /// <summary>
    /// Classify a crash. Returns null when nothing actionable is recognized — the
    /// caller still reports the crash to the backend, we just don't show advice.
    /// </summary>
    public static LauncherCrashInfoDto? Classify(int exitCode, string? logTail, string? crashReport)
    {
        var haystack = ((logTail ?? string.Empty) + "\n" + (crashReport ?? string.Empty));
        var hex = ExitCodeHex(exitCode);

        var rule = Rules.FirstOrDefault(r =>
        {
            try { return r.Matches(exitCode, haystack); }
            catch { return false; }
        });

        // Fall back to a generic advice keyed off the exit code so the player is
        // never left with a bare number.
        var title = rule?.Title ?? "Игра завершилась с ошибкой";
        var cause = rule?.Cause
            ?? (ExitCodeHints.TryGetValue(hex, out var hint)
                ? $"Код завершения {hex}: {hint}."
                : $"Minecraft завершился с кодом {exitCode} ({hex}).");
        var solution = rule?.Solution
            ?? "1) Нажмите «Переустановить», чтобы проверить файлы сборки.\n2) Если ошибка повторяется — пришлите этот отчёт в поддержку.";

        return new LauncherCrashInfoDto
        {
            Id = Guid.NewGuid().ToString("N"),
            ExitCode = exitCode,
            ExitCodeHex = hex,
            Title = title,
            Cause = cause,
            Solution = solution,
            Recognized = rule != null,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}
