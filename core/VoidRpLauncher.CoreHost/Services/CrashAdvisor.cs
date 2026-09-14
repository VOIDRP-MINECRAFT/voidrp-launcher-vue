using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VoidRpLauncher.CoreHost.Contracts;
using VoidRpLauncher.CoreHost.Models;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Turns a raw game crash (exit code + log tail + crash-report file) into a
/// player-friendly explanation with a concrete fix — ideally a button that applies it.
/// Rules are regexes over the log text: built-in ones below (written from real crash
/// reports) plus rules the backend serves, which win on the same key.
/// </summary>
public static class CrashAdvisor
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new();
    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)(?:\|([^}]*))?\}", RegexOptions.CultureInvariant);

    private static CrashRuleAction Act(string type, string label, params string[] paths)
        => new() { Type = type, Label = label, Paths = paths.ToList() };

    private static readonly CrashRuleAction RelaunchAction = Act(CrashActionTypes.Relaunch, "Запустить снова");

    // Built-in rules. Keys are stable: the backend can override or disable them by key.
    public static readonly IReadOnlyList<CrashRule> BuiltInRules = new List<CrashRule>
    {
        // 17 of 43 reports 2026-09-01..13: CrashAssistant migrates a leftover problematic_mods_config.json
        // into a startup script that stops every launch.
        new()
        {
            Key = "crash_assistant_startup_script",
            Priority = 100,
            PatternsAll = { @"Script startup[\\/](?<script>[^\s\\/]+\.jexl) marked game for crash" },
            Title = "Мешает старый файл от прошлой версии сборки",
            Cause = "Игру остановил встроенный модуль CrashAssistant: у вас остался старый скрипт проверки модов ({script}) от прошлой версии сборки. С компьютером и файлами игры всё в порядке.",
            Solution = "Нажмите «Исправить» — лаунчер уберёт этот файл (копия сохранится в папке config-backups). Потом нажмите «Запустить снова».",
            Actions =
            {
                Act(CrashActionTypes.FixFiles, "Исправить",
                    "config/crash_assistant/scripts/startup/{script}",
                    "config/crash_assistant/problematic_mods_config.json",
                    "config/crash_assistant/problematic_mods_config.json.bak"),
                RelaunchAction,
            },
        },
        new()
        {
            Key = "broken_mod_config",
            Priority = 90,
            PatternsAny =
            {
                @"Failed to back up config file (?<file>[^\r\n]+?\.(?:toml|json5?|cfg|properties))",
                @"Failed loading config file (?<file>\S+?\.toml)",
                @"ParsingException[^\r\n]*?(?<file>\S+[\\/]config[\\/]\S+?\.toml)",
            },
            Title = "Сломан файл настроек мода",
            Cause = "Файл настроек {file|одного из модов} повреждён или остался от старой версии мода, и игра не смогла восстановить его сама.",
            Solution = "Нажмите «Сбросить» — лаунчер уберёт только этот файл (копия сохранится в папке config-backups), мод создаст его заново. Потом запустите игру.",
            Actions = { Act(CrashActionTypes.ResetConfig, "Сбросить {file}"), RelaunchAction },
        },
        new()
        {
            Key = "mod_config_not_loaded",
            Priority = 80,
            PatternsAll = { @"Cannot get config value before config is loaded" },
            PatternsAny =
            {
                @"before config is loaded\.[\s\S]{0,1500}?TRANSFORMER/(?!neoforge@|minecraft@)(?<mod>[a-z0-9_]+)@",
                @"before config is loaded",
            },
            Title = "Не загрузились настройки мода",
            Cause = "Мод {mod|из сборки} обратился к своим настройкам раньше, чем они загрузились. Обычно так бывает, когда файлы настроек испорчены или остались от старой версии.",
            Solution = "Нажмите «Сбросить настройки модов» — лаунчер переложит папку config в config-backups и заново скачает настройки сборки. Управление и графика (options.txt) останутся как были.",
            Actions = { Act(CrashActionTypes.ResetAllConfigs, "Сбросить настройки модов"), RelaunchAction },
        },
        // CompactMachines JEI plugin / Extended Industrialization tools hit a null registry while the
        // server sends recipes on join. Harmless for the save; a second join usually works.
        new()
        {
            Key = "join_recipes_mod_bug",
            Priority = 70,
            PatternsAll = { @"ServerLifecycleHooks\.getCurrentServer\(\)"" is null|because ""lookup"" is null" },
            PatternsAny =
            {
                @"TRANSFORMER/(?<mod>compactmachines|extended_industrialization|wom)@",
                @"ServerLifecycleHooks\.getCurrentServer\(\)"" is null|because ""lookup"" is null",
            },
            Title = "Игра упала при входе на сервер",
            Cause = "Это известная ошибка мода {mod|из сборки}: он падает, когда сервер присылает рецепты. Ваш персонаж, вещи и файлы игры в порядке.",
            Solution = "Просто запустите игру и зайдите на сервер ещё раз — обычно со второй попытки всё работает. Мы уже чиним эту ошибку.",
            Actions = { RelaunchAction },
        },
        new()
        {
            Key = "sodium_iris_not_ready",
            Priority = 60,
            PatternsAll = { @"Mod with id iris not found in ModList" },
            Title = "Графика не успела загрузиться",
            Cause = "Моды графики Sodium и Iris не успели запуститься. Чаще всего так бывает, когда игре выделено мало оперативной памяти.",
            Solution = "Откройте настройки и выделите игре 6–8 ГБ памяти. Если на компьютере всего 8 ГБ — закройте браузер перед запуском. Потом запустите игру снова.",
            Actions = { Act(CrashActionTypes.OpenSettings, "Настроить память"), RelaunchAction },
        },
        new()
        {
            Key = "pack_files_mismatch",
            Priority = 55,
            PatternsAny = { @"Missing or unsupported mandatory dependencies", @"requires \S+ [\w.+-]+ or above" },
            Title = "Файлы сборки устарели",
            Cause = "Некоторым модам не хватает других модов нужной версии: файлы сборки на компьютере устарели или скачались не полностью.",
            Solution = "Нажмите «Починить клиент» — лаунчер проверит и перекачает файлы сборки. Затем запустите игру.",
            Actions = { Act(CrashActionTypes.Repair, "Починить клиент"), RelaunchAction },
        },
        // Mod jars vanished / were locked mid-scan: classic antivirus quarantine.
        new()
        {
            Key = "antivirus_locked_files",
            Priority = 50,
            PatternsAny =
            {
                @"setup the game directory",
                @"(?:NoSuchFile|AccessDenied)Exception: [^\r\n]*[\\/]mods[\\/]",
            },
            Title = "Антивирус заблокировал файлы сборки",
            Cause = "Во время запуска часть модов в папке mods оказалась недоступна (файлы удалены или заблокированы). Обычно это антивирус (чаще всего Windows Defender), который держит или помещает .jar-моды в карантин.",
            Solution = "1) Добавьте папку лаунчера в исключения антивируса: %LOCALAPPDATA%\\VoidRpLauncher\n2) Нажмите «Починить клиент», чтобы докачать файлы заново.\n3) Запустите игру снова.",
            Actions = { Act(CrashActionTypes.Repair, "Починить клиент"), RelaunchAction },
        },
        new()
        {
            Key = "out_of_memory",
            Priority = 45,
            PatternsAny = { @"OutOfMemoryError", @"java heap space", @"GC overhead limit" },
            Title = "Не хватает оперативной памяти",
            Cause = "Игре не хватило выделенной оперативной памяти (OutOfMemoryError). Для этой сборки выделено слишком мало ОЗУ.",
            Solution = "Откройте настройки и увеличьте выделяемую память (рекомендуется 6–8 ГБ). Если на компьютере всего 8 ГБ — закройте браузер и другие программы перед запуском.",
            Actions = { Act(CrashActionTypes.OpenSettings, "Настроить память"), RelaunchAction },
        },
        new()
        {
            Key = "gpu_driver",
            Priority = 40,
            PatternsAny =
            {
                @"Couldn't set pixel format",
                @"Failed to create window",
                @"Requested GL version \S+ got version (?:[12]\.|3\.[01])",
                @"EXCEPTION_ACCESS_VIOLATION[\s\S]*?(?:nvoglv|atio6axx|igdumd|ig\d+icd)",
            },
            Title = "Проблема с драйвером видеокарты",
            Cause = "Не удалось запустить графику нужной версии. Обычно причина — устаревший драйвер видеокарты или запуск на встроенной графике.",
            Solution = "1) Обновите драйвер видеокарты (NVIDIA/AMD/Intel) до последней версии.\n2) На ноутбуке убедитесь, что игра запускается на дискретной видеокарте.\n3) Перезагрузите компьютер и попробуйте снова.",
            Actions = { RelaunchAction },
        },
        new()
        {
            Key = "window_zero_size",
            Priority = 38,
            PatternsAll = { @"Window 0x0 size out of bounds" },
            Title = "Окно игры было свёрнуто при запуске",
            Cause = "Игра не смогла создать окно нулевого размера. Обычно так бывает, если свернуть окно во время загрузки или если сохранился неверный размер окна.",
            Solution = "Запустите игру снова и не сворачивайте окно, пока идёт загрузка. Если повторяется — выключите полноэкранный режим в настройках игры.",
            Actions = { RelaunchAction },
        },
        new()
        {
            Key = "mcef_native_crash",
            Priority = 35,
            ExitCodes = { -1073740791 },
            Title = "Сбой встроенного браузера (MCEF)",
            Cause = "Игра упала из-за встроенного браузера MCEF/CEF (код 0xC0000409). Обычно это разовый сбой или заблокированные антивирусом файлы CEF.",
            Solution = "1) Запустите игру ещё раз — часто помогает.\n2) Если повторяется — добавьте папку лаунчера в исключения антивируса и нажмите «Починить клиент».",
            Actions = { RelaunchAction, Act(CrashActionTypes.Repair, "Починить клиент") },
        },
        new()
        {
            Key = "mod_loading_failed",
            Priority = 10,
            PatternsAny = { @"Failed to load mods", @"Mod loading has failed", @"ModLoadingException" },
            Title = "Ошибка в одном из модов",
            Cause = "Один из модов сборки остановил игру при загрузке. Часто это следствие повреждённого файла мода или незавершённого обновления.",
            Solution = "1) Нажмите «Починить клиент» — лаунчер перекачает и проверит файлы.\n2) Если не помогло — скопируйте отчёт и пришлите его в поддержку.",
            Actions = { Act(CrashActionTypes.Repair, "Починить клиент"), RelaunchAction },
        },
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

    /// <summary>Built-in rules merged with backend rules: same key replaces, disabled removes.</summary>
    public static List<CrashRule> EffectiveRules(IReadOnlyList<CrashRule>? remoteRules)
    {
        var remote = (remoteRules ?? Array.Empty<CrashRule>())
            .Where(r => r is not null && !string.IsNullOrWhiteSpace(r.Key))
            .Select(Sanitize)
            .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var merged = BuiltInRules
            .Select(r => remote.TryGetValue(r.Key, out var over) ? over : r)
            .Concat(remote.Values.Where(r => !BuiltInRules.Any(b => string.Equals(b.Key, r.Key, StringComparison.OrdinalIgnoreCase))))
            .Where(r => r.Enabled)
            .ToList();

        // Stable sort: equal priorities keep built-ins first.
        return merged.Select((r, i) => (r, i)).OrderByDescending(x => x.r.Priority).ThenBy(x => x.i).Select(x => x.r).ToList();
    }

    // JSON from the server may carry nulls where the model expects empty lists/strings.
    private static CrashRule Sanitize(CrashRule rule)
    {
        rule.PatternsAll = (rule.PatternsAll ?? new()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        rule.PatternsAny = (rule.PatternsAny ?? new()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        rule.ExitCodes ??= new();
        rule.Title ??= string.Empty;
        rule.Cause ??= string.Empty;
        rule.Solution ??= string.Empty;
        rule.Actions = (rule.Actions ?? new()).Where(a => a is not null).ToList();
        foreach (var action in rule.Actions)
        {
            action.Label ??= string.Empty;
            action.Paths ??= new();
        }
        return rule;
    }

    /// <summary>
    /// Classify a crash. Always returns advice; <see cref="LauncherCrashInfoDto.Recognized"/>
    /// tells whether a rule matched. <paramref name="repeatCount"/> escalates the advice when
    /// the same failure keeps happening.
    /// </summary>
    public static LauncherCrashInfoDto Classify(int exitCode, string? logTail, string? crashReport,
        IReadOnlyList<CrashRule>? rules = null, int repeatCount = 1)
    {
        var haystack = (logTail ?? string.Empty) + "\n" + (crashReport ?? string.Empty);
        var hex = ExitCodeHex(exitCode);

        CrashRule? rule = null;
        Dictionary<string, string> captures = new(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in rules ?? EffectiveRules(null))
        {
            if (TryMatch(candidate, exitCode, haystack, out var found))
            {
                rule = candidate;
                captures = found;
                break;
            }
        }

        var info = new LauncherCrashInfoDto
        {
            Id = Guid.NewGuid().ToString("N"),
            ExitCode = exitCode,
            ExitCodeHex = hex,
            Recognized = rule != null,
            RuleKey = rule?.Key,
            RepeatCount = Math.Max(1, repeatCount),
            DetectedAt = DateTimeOffset.UtcNow,
        };

        if (rule != null)
        {
            info.Title = Render(rule.Title, captures);
            info.Cause = Render(rule.Cause, captures);
            info.Solution = Render(rule.Solution, captures);
            info.Actions = BuildActions(rule.Actions, captures);
        }
        else
        {
            // Never leave the player with a bare number.
            info.Title = "Игра завершилась с ошибкой";
            info.Cause = ExitCodeHints.TryGetValue(hex, out var hint)
                ? $"Код завершения {hex}: {hint}."
                : $"Minecraft завершился с кодом {exitCode} ({hex}).";
            info.Solution = "1) Запустите игру ещё раз.\n2) Если ошибка повторяется — нажмите «Починить клиент».\n3) Не помогло — скопируйте отчёт и пришлите его в поддержку.";
            info.Actions = BuildActions(new List<CrashRuleAction> { RelaunchAction, Act(CrashActionTypes.Repair, "Починить клиент") }, captures);
        }

        ApplyRepeat(info, repeatCount);
        return info;
    }

    /// <summary>Marks how often this failure repeated; from the second time on the advice escalates.</summary>
    public static void ApplyRepeat(LauncherCrashInfoDto info, int repeatCount)
    {
        info.RepeatCount = Math.Max(1, repeatCount);
        if (info.RepeatCount < 2) return;

        info.Cause += $"\n\nЭто уже {info.RepeatCount}-й такой сбой за сутки.";
        info.Solution += "\n\nЕсли не помогает — нажмите «Скопировать отчёт» и отправьте его нам в Discord или Telegram, разберёмся.";

        if (info.Actions.All(a => a.Type != CrashActionTypes.Repair))
            info.Actions.Add(new CrashActionDto { Type = CrashActionTypes.Repair, Label = "Починить клиент" });
        if (info.Actions.All(a => a.Type != CrashActionTypes.CopyReport))
            info.Actions.Add(new CrashActionDto { Type = CrashActionTypes.CopyReport, Label = "Скопировать отчёт" });

        for (var i = 0; i < info.Actions.Count; i++)
            info.Actions[i].Index = i;
    }

    private static bool TryMatch(CrashRule rule, int exitCode, string haystack, out Dictionary<string, string> captures)
    {
        captures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rule.ExitCodes.Count > 0 && !rule.ExitCodes.Contains(exitCode)) return false;
        if (rule.PatternsAll.Count == 0 && rule.PatternsAny.Count == 0 && rule.ExitCodes.Count == 0) return false;

        foreach (var pattern in rule.PatternsAll)
        {
            if (!TryRegex(pattern, haystack, captures)) return false;
        }

        if (rule.PatternsAny.Count > 0)
        {
            // Evaluate every pattern (no short-circuit) so capture-only patterns still fill placeholders.
            var anyMatched = false;
            foreach (var pattern in rule.PatternsAny)
                anyMatched |= TryRegex(pattern, haystack, captures);
            if (!anyMatched) return false;
        }

        return true;
    }

    private static bool TryRegex(string pattern, string haystack, Dictionary<string, string> captures)
    {
        var regex = RegexCache.GetOrAdd(pattern, p =>
        {
            try { return new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout); }
            catch (ArgumentException) { return null; }   // a broken server-side pattern just never matches
        });
        if (regex is null) return false;

        Match match;
        try { match = regex.Match(haystack); }
        catch (RegexMatchTimeoutException) { return false; }
        if (!match.Success) return false;

        foreach (var name in regex.GetGroupNames())
        {
            if (int.TryParse(name, out _) || captures.ContainsKey(name)) continue;
            var group = match.Groups[name];
            if (!group.Success || string.IsNullOrWhiteSpace(group.Value)) continue;

            if (string.Equals(name, "file", StringComparison.OrdinalIgnoreCase))
            {
                // Log lines carry absolute Windows paths; keep only the part inside config/.
                var inConfig = ToConfigRelative(group.Value);
                if (inConfig is not null) captures[name] = inConfig;
            }
            else
            {
                captures[name] = group.Value.Trim();
            }
        }

        return true;
    }

    /// <summary>"C:\...\game\config\malum-client.toml" → "malum-client.toml"; null when not inside config/.</summary>
    public static string? ToConfigRelative(string raw)
    {
        var p = raw.Trim().Trim('"', '\'').Replace('\\', '/');
        string? rest;
        var gameIdx = p.IndexOf("/game/config/", StringComparison.OrdinalIgnoreCase);
        var cfgIdx = p.LastIndexOf("/config/", StringComparison.OrdinalIgnoreCase);
        if (gameIdx >= 0) rest = p[(gameIdx + "/game/config/".Length)..];
        else if (cfgIdx >= 0) rest = p[(cfgIdx + "/config/".Length)..];
        else if (p.StartsWith("config/", StringComparison.OrdinalIgnoreCase)) rest = p["config/".Length..];
        else rest = p.Contains('/') || p.Contains(':') ? null : p;

        if (string.IsNullOrWhiteSpace(rest) || rest.Contains("..") || rest.Contains(':') || rest.StartsWith('/')) return null;
        return rest;
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> captures)
        => PlaceholderRegex.Replace(template ?? string.Empty, m =>
            captures.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Groups[2].Value);

    private static List<CrashActionDto> BuildActions(IEnumerable<CrashRuleAction> actions, IReadOnlyDictionary<string, string> captures)
    {
        var result = new List<CrashActionDto>();
        foreach (var action in actions)
        {
            var type = (action.Type ?? string.Empty).Trim().ToLowerInvariant();
            if (!CrashActionTypes.All.Contains(type)) continue;

            var dto = new CrashActionDto { Type = type, Label = Render(action.Label, captures) };
            switch (type)
            {
                case CrashActionTypes.FixFiles:
                    dto.Paths = action.Paths
                        .Select(p => SafeConfigPath(Render(p, captures)))
                        .Where(p => p is not null)
                        .Select(p => p!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (dto.Paths.Count == 0) continue;
                    break;

                case CrashActionTypes.ResetConfig:
                    if (captures.TryGetValue("file", out var file) && SafeConfigPath("config/" + file) is { } path)
                    {
                        dto.Paths = new List<string> { path };
                    }
                    else
                    {
                        // The log did not name the file: offer the whole-folder reset instead.
                        dto.Type = CrashActionTypes.ResetAllConfigs;
                        dto.Label = "Сбросить настройки модов";
                    }
                    break;
            }

            if (string.IsNullOrWhiteSpace(dto.Label)) dto.Label = dto.Type;
            if (result.Any(r => r.Type == dto.Type && r.Paths.SequenceEqual(dto.Paths))) continue;
            result.Add(dto);
        }

        for (var i = 0; i < result.Count; i++)
            result[i].Index = i;
        return result;
    }

    /// <summary>Game-dir relative path confined to config/, or null.</summary>
    public static string? SafeConfigPath(string path)
    {
        var p = (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        if (!p.StartsWith("config/", StringComparison.OrdinalIgnoreCase) || p.Length <= "config/".Length) return null;
        if (p.Contains("..") || p.Contains(':') || p.Contains("//") || p.Contains('{')) return null;
        return p;
    }
}
