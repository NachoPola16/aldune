using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aldune.Core.Tests;

/// <summary>
/// Vectores del formato de texto, el color y el orden (<c>tests/vectors/text-lines.json</c>, <c>text-color.json</c>,
/// <c>text-order.json</c>): operación + entrada (+ cursor) → salida de <c>Aldune.Core</c>. Son el contrato de <c>android/text</c>
/// (Kotlin), igual que <c>sync-scenarios.json</c> lo es de <c>android/sync</c>. Ver la spec de Android, §5 (F3).
///
/// Mismo patrón que <see cref="SyncScenarioVectorsTests"/>: las ENTRADAS salen de <see cref="Build"/> y se guardan en el fichero;
/// una ejecución normal lee las entradas del fichero, ejecuta Core y compara con lo anotado, de modo que el fichero solo cambia
/// si se regenera a propósito: <c>ALDUNE_REGEN_VECTORS=1 dotnet test tests/Aldune.Core.Tests --filter TextVectorsTests</c>.
/// Core no se toca: lo anotado es lo que hace hoy, también sus rarezas.
///
/// Los desplazamientos se miden en unidades UTF-16 (<c>string.Length</c>), igual que en Kotlin. Los números reales se escriben
/// con el formato de ida y vuelta de .NET (el más corto que recupera el mismo <c>double</c>).
/// </summary>
public sealed class TextVectorsTests
{
    private const string LinesFile = "text-lines.json";
    private const string ColorFile = "text-color.json";
    private const string OrderFile = "text-order.json";

    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string VectorsPath(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aldune.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Aldune.slnx not found"), "tests", "vectors", file);
    }

    [Theory]
    [InlineData(LinesFile, 1500)]
    [InlineData(ColorFile, 500)]
    [InlineData(OrderFile, 40)]
    public void Vectors_MatchTheRecordedBehaviour(string file, int minimumCases)
    {
        var cases = JsonNode.Parse(File.ReadAllText(VectorsPath(file)))!["cases"]!.AsArray();
        Assert.True(cases.Count >= minimumCases, $"Faltan casos en {file}: {cases.Count}.");
        var failures = new List<string>();
        foreach (var c in cases)
        {
            var observed = Observe(c!.AsObject());
            if (!JsonNode.DeepEquals(c["expected"], observed))
                failures.Add($"{c["op"]} {c["args"]!.ToJsonString(Relaxed)}\n  esperado: {c["expected"]?.ToJsonString(Relaxed)}\n  observado: {observed?.ToJsonString(Relaxed)}");
            if (failures.Count >= 10) break;
        }
        Assert.True(failures.Count == 0, $"{file}:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void RegenerateTextVectors_OnlyWhenAsked()
    {
        if (Environment.GetEnvironmentVariable("ALDUNE_REGEN_VECTORS") != "1") return;
        Write(LinesFile, "Vectores del formato de texto de Aldune (tareas, viñetas, sangría, mover líneas, título, texto de nota). " +
                         "Ver tests/vectors/README.md.", LinesCases());
        Write(ColorFile, "Vectores de color de Aldune (OKLCH, contraste, derivación de borde y etiqueta, temas, reparto de color). " +
                         "Ver tests/vectors/README.md.", ColorCases());
        Write(OrderFile, "Vectores del orden manual del mazo (NoteOrdering). Ver tests/vectors/README.md.", OrderCases());
    }

    private static void Write(string file, string notes, IEnumerable<JsonObject> cases)
    {
        // Un caso por línea: diffs legibles y ficheros que no se hinchan con sangrías.
        var builder = new StringBuilder();
        builder.Append("{\n  \"notes\": ").Append(JsonSerializer.Serialize(notes, Relaxed)).Append(",\n  \"cases\": [\n");
        bool first = true;
        foreach (var c in cases)
        {
            c["expected"] = Observe(c);
            if (!first) builder.Append(",\n");
            first = false;
            builder.Append("    ").Append(c.ToJsonString(Relaxed));
        }
        builder.Append("\n  ]\n}\n");
        File.WriteAllText(VectorsPath(file), builder.ToString(), new UTF8Encoding(false));
    }

    // ───────────────────────── ejecutor ─────────────────────────

    /// <summary>Ejecuta el caso y devuelve el resultado ya pasado por JSON (ida y vuelta), para compararlo con lo guardado.</summary>
    private static JsonNode? Observe(JsonObject c)
    {
        var node = ToNode(Execute(c["op"]!.GetValue<string>(), c["args"]!.AsObject()));
        return node is null ? null : JsonNode.Parse(node.ToJsonString(Relaxed));
    }

    private static string S(JsonObject a, string key) => a[key]!.GetValue<string>();
    private static int I(JsonObject a, string key) => a[key]!.GetValue<int>();
    private static double D(JsonObject a, string key) => a[key]!.GetValue<double>();
    private static string? NS(JsonObject a, string key) => a[key]?.GetValue<string>();
    private static double? ND(JsonObject a, string key) => a[key]?.GetValue<double>();
    private static List<string> Strings(JsonObject a, string key) => a[key]!.AsArray().Select(x => x!.GetValue<string>()).ToList();

    private static object? Execute(string op, JsonObject a) => op switch
    {
        "TaskLines.GlyphIndex" => TaskLines.GlyphIndex(S(a, "line")),
        "TaskLines.IsTaskLine" => TaskLines.IsTaskLine(S(a, "line")),
        "TaskLines.IsChecked" => TaskLines.IsChecked(S(a, "line")),
        "TaskLines.PrefixLength" => TaskLines.PrefixLength(S(a, "line"), I(a, "glyphIndex")),
        "TaskLines.IsEmptyTaskLine" => TaskLines.IsEmptyTaskLine(S(a, "line")),
        "TaskLines.LineContaining" => TaskLines.LineContaining(S(a, "text"), I(a, "index")),
        "TaskLines.LineStart" => TaskLines.LineStart(S(a, "text"), I(a, "index")),
        "TaskLines.ToggleCheckboxAt" => TaskLines.ToggleCheckboxAt(S(a, "text"), I(a, "index")),
        "TaskLines.ToggleTaskLineAt" => TaskLines.ToggleTaskLineAt(S(a, "text"), I(a, "caret")),
        "TaskLines.EnterContinuation" => TaskLines.EnterContinuation(S(a, "text"), I(a, "caret"), S(a, "newLine")),
        "TaskLines.Count" => TaskLines.Count(S(a, "text")),

        "BulletLines.GlyphIndex" => BulletLines.GlyphIndex(S(a, "line")),
        "BulletLines.IsBulletLine" => BulletLines.IsBulletLine(S(a, "line")),
        "BulletLines.PrefixLength" => BulletLines.PrefixLength(S(a, "line"), I(a, "glyphIndex")),
        "BulletLines.IsEmptyBulletLine" => BulletLines.IsEmptyBulletLine(S(a, "line")),
        "BulletLines.ToggleBulletLineAt" => BulletLines.ToggleBulletLineAt(S(a, "text"), I(a, "caret")),
        "BulletLines.EnterContinuation" => BulletLines.EnterContinuation(S(a, "text"), I(a, "caret"), S(a, "newLine")),

        "ListPrefix.RemoveOnBackspace" => ListPrefix.RemoveOnBackspace(S(a, "text"), I(a, "caret")),

        "ListIndent.Indent" => ListIndent.Indent(S(a, "text"), I(a, "caret")),
        "ListIndent.Outdent" => ListIndent.Outdent(S(a, "text"), I(a, "caret")),
        "ListIndent.IndentLines" => ListIndent.IndentLines(S(a, "text"), I(a, "selectionStart"), I(a, "selectionLength")),
        "ListIndent.OutdentLines" => ListIndent.OutdentLines(S(a, "text"), I(a, "selectionStart"), I(a, "selectionLength")),
        "ListIndent.IsListLineAt" => ListIndent.IsListLineAt(S(a, "text"), I(a, "caret")),

        "LineMovement.Move" => LineMovement.Move(S(a, "text"), I(a, "caret"), (LineDirection)I(a, "direction")),

        "TaskLists.CheckboxOnLine" => TaskLists.CheckboxOnLine(S(a, "text"), I(a, "caret")),
        "TaskLists.HasChecked" => TaskLists.HasChecked(S(a, "text")),
        "TaskLists.IsAllDone" => TaskLists.IsAllDone(S(a, "text")),
        "TaskLists.UncheckAll" => Edit(TaskLists.UncheckAll(S(a, "text")), S(a, "text")),
        "TaskLists.RemoveChecked" => Edit(TaskLists.RemoveChecked(S(a, "text")), S(a, "text")),
        "TaskLists.SettleToggled" => Edit(TaskLists.SettleToggled(S(a, "text"), I(a, "glyphIndex")), S(a, "text")),
        "TaskLists.ConvertMarkdownTasks" => TaskLists.ConvertMarkdownTasks(S(a, "text")),
        "TaskLists.PendingTasks" => TaskLists.PendingTasks(S(a, "text"), I(a, "max")),

        "NoteTitleHelper.GetTitle" => NoteTitleHelper.GetTitle(S(a, "text")),
        "NoteTitleHelper.GetPreview" => NoteTitleHelper.GetPreview(S(a, "text")),
        "NoteTitleHelper.GetTabPreview" => NoteTitleHelper.GetTabPreview(S(a, "text")),
        "NoteText.Split" => NoteText.Split(S(a, "text")),
        "NoteText.Join" => NoteText.Join(S(a, "title"), S(a, "body")),

        "OklchColor.FromHex" => OklchColor.TryFromHex(NS(a, "hex"), out var parsed) ? parsed : null,
        "OklchColor.ToHex" => new OklchColor(D(a, "l"), D(a, "c"), D(a, "h")).ToHex(),
        "OklchColor.DistanceTo" => new OklchColor(D(a, "l"), D(a, "c"), D(a, "h")).DistanceTo(new OklchColor(D(a, "l2"), D(a, "c2"), D(a, "h2"))),
        "NoteColorContrast.ForegroundFor" => NoteColorContrast.ForegroundFor(NS(a, "background")),
        "NoteColorContrast.IsReadable" => NoteColorContrast.IsReadable(NS(a, "background"), NS(a, "foreground")),
        "NoteColorContrast.Luminance" => NoteColorContrast.TryGetLuminance(NS(a, "color"), out var lum) ? lum : null,
        "NoteColorDerivation.IsDark" => NoteColorDerivation.IsDark(NS(a, "color")),
        "NoteColorDerivation.RimFor" => NoteColorDerivation.RimFor(S(a, "color")),
        "NoteColorDerivation.LabelFor" => NoteColorDerivation.LabelFor(S(a, "color")),
        "NoteColorDerivation.RestOutlineFor" => NoteColorDerivation.RestOutlineFor(NS(a, "color")),
        "NoteColorDerivation.StripeColor" => NoteColorDerivation.StripeColor(S(a, "color"), S(a, "ground")),
        "NoteColorDerivation.ClassicColors" => NoteColorDerivation.ClassicColors,
        "NoteDisplayColor.Resolve" => NoteDisplayColor.Resolve(S(a, "color"), NS(a, "uniform")),
        "NoteThemes.BuiltIn" => NoteThemes.BuiltIn.Select(ThemeNode).ToList(),
        "NoteThemes.Legacy" => new JsonObject
        {
            ["dark"] = Array(NoteThemes.LegacySereneDarkColors), ["light"] = Array(NoteThemes.LegacySereneLightColors),
            ["recolor"] = new JsonObject(NoteThemes.SereneRecolorMap.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value)))
        },
        "NoteThemes.Resolve" => ThemeNode(NoteThemes.Resolve(NS(a, "id"), CustomThemes(a))),
        "NoteThemes.Sanitize" => NoteThemes.Sanitize(CustomThemes(a)).Select(ThemeNode).ToList(),
        "NoteColorAssigner.Assign" => NoteColorAssigner.Assign(ThemeOf(a), (NoteTone)I(a, "tone"), (NoteColorAssignment)I(a, "rule"),
            NS(a, "fixedColor"), Strings(a, "dockColors")),

        "NoteOrdering.Between" => NoteOrdering.Between(ND(a, "before"), ND(a, "after")),
        "NoteOrdering.Spaced" => NoteOrdering.Spaced(I(a, "count")),
        "NoteOrdering.SlotShift" => NoteOrdering.SlotShift(D(a, "delta"), D(a, "pitch"), a["hysteresis"] is null ? NoteOrdering.SlotHysteresis : D(a, "hysteresis")),
        "NoteOrdering.TargetIndex" => NoteOrdering.TargetIndex(I(a, "originalIndex"), D(a, "delta"), D(a, "pitch"), I(a, "count")),
        _ => throw new InvalidOperationException($"Operación desconocida: {op}")
    };

    private static JsonArray Array(IEnumerable<string> values) => new(values.Select(v => (JsonNode?)v).ToArray());

    private static JsonObject ThemeNode(NoteTheme t) => new()
    {
        ["id"] = t.Id, ["name"] = t.Name, ["isBuiltIn"] = t.IsBuiltIn, ["dark"] = Array(t.DarkColors), ["light"] = Array(t.LightColors)
    };

    private static NoteTheme ThemeFrom(JsonObject o) => new()
    {
        Id = o["id"]!.GetValue<string>(), Name = o["name"]?.GetValue<string>() ?? "",
        DarkColors = o["dark"]!.AsArray().Select(x => x!.GetValue<string>()).ToList(),
        LightColors = o["light"]!.AsArray().Select(x => x!.GetValue<string>()).ToList()
    };

    private static List<NoteTheme>? CustomThemes(JsonObject a) =>
        a["custom"]?.AsArray().Select(x => ThemeFrom(x!.AsObject())).ToList();

    private static NoteTheme ThemeOf(JsonObject a) => a["theme"] is JsonObject t
        ? ThemeFrom(t)
        : NoteThemes.BuiltIn.First(x => x.Id == S(a, "themeId"));

    private static JsonNode Edit(TextEdit e, string original)
    {
        // Para cada posición del texto original, dónde queda en el nuevo: lo que usa la interfaz para no mover el cursor.
        var map = new JsonArray();
        for (int i = 0; i <= original.Length; i++) map.Add(e.MapIndex(i));
        return new JsonObject { ["text"] = e.Text, ["changed"] = e.Changed, ["map"] = map };
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        double d => JsonValue.Create(d),
        ValueTuple<string, int> t => new JsonObject { ["text"] = t.Item1, ["caret"] = t.Item2 },
        ValueTuple<int, int> t => new JsonObject { ["done"] = t.Item1, ["total"] = t.Item2 },
        ValueTuple<string, string> t => new JsonObject { ["title"] = t.Item1, ["body"] = t.Item2 },
        ValueTuple<string, int, int> t => new JsonObject { ["text"] = t.Item1, ["selectionStart"] = t.Item2, ["selectionLength"] = t.Item3 },
        OklchColor c => new JsonObject { ["l"] = c.L, ["c"] = c.C, ["h"] = c.H },
        IEnumerable<double> doubles => new JsonArray(doubles.Select(x => (JsonNode?)x).ToArray()),
        IEnumerable<string> strings => new JsonArray(strings.Select(x => (JsonNode?)x).ToArray()),
        IEnumerable<JsonObject> objects => new JsonArray(objects.Select(x => (JsonNode?)x).ToArray()),
        _ => throw new InvalidOperationException($"Tipo sin traducir: {value.GetType()}")
    };

    // ───────────────────────── construcción de las entradas ─────────────────────────

    private static JsonObject Case(string op, params (string Key, object? Value)[] args)
    {
        var a = new JsonObject();
        foreach (var (key, value) in args) a[key] = value is null ? null : ToNode(value);
        return new JsonObject { ["op"] = op, ["args"] = a };
    }

    private static IEnumerable<int> AllCarets(string text)
    {
        // Todos los cursores posibles, salvo el que parte un par sustituto (un cursor de verdad no se queda ahí), más dos fuera de rango
        // para fijar el recorte.
        yield return -1;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i > 0 && i < text.Length && char.IsHighSurrogate(text[i - 1])) continue;
            yield return i;
        }
        yield return text.Length + 5;
    }

    // Textos de prueba: los glifos (☐ ☒ ☑ →), con y sin espacio detrás, con sangría de espacios y de tabulador, líneas vacías,
    // saltos LF y CRLF, un emoji (par sustituto) y acentos.
    private static readonly string[] Texts =
    [
        "", "x", "hola mundo", "\n", "\n\n", "a\nb", "a\r\nb",
        "☐ comprar leche", "☒ hecho", "☑ de fuera", "☐ ", "☐", "☐x", "☒x", "→ viñeta", "→ ", "→", "→x",
        "→ ☐ viñeta con casilla", "→ ☐ ", "    ☐ sangrada", "\t☐ con tab", "  → sub", "    ", "   ☒ a",
        "línea 1\n☐ t1\n☐ t2\nfin", "☐ a\r\n☐ b\r\n→ c\r\n", "☐ a\n\n☐ b", "☐ 😀 ok", "→ ☐ \n", "    ☐ \n  → x",
        "# Título\ncuerpo", "☐ a\r\n☐ b", "texto\n  ☒ hecha\n  ☐ pendiente"
    ];

    private static IEnumerable<JsonObject> LinesCases()
    {
        // Líneas sueltas (sin salto) para los predicados.
        string[] lines =
        [
            "", " ", "x", "☐", "☐ ", "☐x", "☐ x", "☒ x", "☑ x", "☒x", "  ☐ x", "\t☐ x", "→", "→ ", "→x", "→ x", "  → x", "→ ☐ x", "→ ☒ x",
            "→ ☐", "→ ☐x", "x ☐ y", "x → y", "☐  dos espacios", "☐ \t", "→ ☐ \t", "    ☐ ", "# ☐ x", "😀 ☐ x", "☐ 😀"
        ];
        foreach (var line in lines)
        {
            yield return Case("TaskLines.GlyphIndex", ("line", line));
            yield return Case("TaskLines.IsTaskLine", ("line", line));
            yield return Case("TaskLines.IsChecked", ("line", line));
            yield return Case("TaskLines.IsEmptyTaskLine", ("line", line));
            yield return Case("BulletLines.GlyphIndex", ("line", line));
            yield return Case("BulletLines.IsBulletLine", ("line", line));
            yield return Case("BulletLines.IsEmptyBulletLine", ("line", line));
            for (int g = 0; g <= Math.Min(line.Length, 6); g++)
            {
                yield return Case("TaskLines.PrefixLength", ("line", line), ("glyphIndex", g));
                yield return Case("BulletLines.PrefixLength", ("line", line), ("glyphIndex", g));
            }
        }

        foreach (var text in Texts)
        {
            yield return Case("TaskLines.Count", ("text", text));
            yield return Case("TaskLists.HasChecked", ("text", text));
            yield return Case("TaskLists.IsAllDone", ("text", text));
            yield return Case("TaskLists.UncheckAll", ("text", text));
            yield return Case("TaskLists.RemoveChecked", ("text", text));
            yield return Case("TaskLists.ConvertMarkdownTasks", ("text", text));
            yield return Case("TaskLists.PendingTasks", ("text", text), ("max", 0));
            yield return Case("TaskLists.PendingTasks", ("text", text), ("max", 2));
            yield return Case("TaskLists.PendingTasks", ("text", text), ("max", 10));
            yield return Case("NoteTitleHelper.GetTitle", ("text", text));
            yield return Case("NoteTitleHelper.GetPreview", ("text", text));
            yield return Case("NoteTitleHelper.GetTabPreview", ("text", text));
            yield return Case("NoteText.Split", ("text", text));

            foreach (var caret in AllCarets(text))
            {
                yield return Case("TaskLines.ToggleTaskLineAt", ("text", text), ("caret", caret));
                yield return Case("TaskLines.EnterContinuation", ("text", text), ("caret", caret), ("newLine", "\r\n"));
                yield return Case("BulletLines.ToggleBulletLineAt", ("text", text), ("caret", caret));
                yield return Case("BulletLines.EnterContinuation", ("text", text), ("caret", caret), ("newLine", "\r\n"));
                yield return Case("ListPrefix.RemoveOnBackspace", ("text", text), ("caret", caret));
                yield return Case("ListIndent.Indent", ("text", text), ("caret", caret));
                yield return Case("ListIndent.Outdent", ("text", text), ("caret", caret));
                yield return Case("ListIndent.IsListLineAt", ("text", text), ("caret", caret));
                yield return Case("LineMovement.Move", ("text", text), ("caret", caret), ("direction", 0));
                yield return Case("LineMovement.Move", ("text", text), ("caret", caret), ("direction", 1));
                yield return Case("TaskLists.CheckboxOnLine", ("text", text), ("caret", caret));
                yield return Case("TaskLines.LineContaining", ("text", text), ("index", caret));
                yield return Case("TaskLines.LineStart", ("text", text), ("index", caret));
                yield return Case("TaskLines.ToggleCheckboxAt", ("text", text), ("index", caret));
                yield return Case("TaskLists.SettleToggled", ("text", text), ("glyphIndex", caret));
            }
        }

        // Enter con el salto LF (lo que usa Android por dentro).
        foreach (var text in Texts.Where(t => t.Length > 0 && t.Length < 40))
            foreach (var caret in new[] { 0, text.Length / 2, text.Length })
            {
                yield return Case("TaskLines.EnterContinuation", ("text", text), ("caret", caret), ("newLine", "\n"));
                yield return Case("BulletLines.EnterContinuation", ("text", text), ("caret", caret), ("newLine", "\n"));
            }

        // Selecciones de varias líneas (Tab y Mayús+Tab sobre una selección).
        string[] selectionTexts =
        [
            "a\nb\nc", "☐ a\n☐ b\n\n→ c", "  x\r\n\ty\r\n z", "    ☐ a\n  → b\n\n    c", "uno\r\n\r\ndos\r\n", "\n\n"
        ];
        foreach (var text in selectionTexts)
            for (int start = 0; start <= text.Length; start++)
                foreach (var length in new[] { 0, 1, 3, 6, text.Length - start })
                {
                    if (length < 0 || start + length > text.Length + 2) continue;
                    yield return Case("ListIndent.IndentLines", ("text", text), ("selectionStart", start), ("selectionLength", length));
                    yield return Case("ListIndent.OutdentLines", ("text", text), ("selectionStart", start), ("selectionLength", length));
                }
        yield return Case("ListIndent.IndentLines", ("text", "a\nb"), ("selectionStart", -3), ("selectionLength", 99));
        yield return Case("ListIndent.OutdentLines", ("text", "  a\n  b"), ("selectionStart", 2), ("selectionLength", -4));

        // Listas de tareas: casos con significado (subtareas, listas mezcladas, hechas arriba y abajo).
        string[] lists =
        [
            "☐ a\n☐ b\n☐ c", "☒ a\n☐ b\n☐ c", "☐ a\n☒ b\n☒ c\n☐ d", "☒ a\n☒ b\n☐ c", "☐ a\n    ☐ a1\n☐ b", "☒ a\n    ☒ a1\n☐ b",
            "☐ a\r\n☒ b\r\n☐ c\r\n", "☐ a\n\n☒ b\n☐ c", "tít\n☒ a\n☐ b\ntexto\n☒ c\n☐ d", "→ ☐ a\n→ ☒ b\n→ ☐ c", "  ☐ a\n  ☒ b\n☐ c",
            "- [ ] a\n- [x] b\n* [X] c\n+ [ ] d\n[ ] e\n[x]f\n  - [ ] g\n\t[x] h\n- [] no\n-[ ] raro"
        ];
        foreach (var text in lists)
        {
            yield return Case("TaskLines.Count", ("text", text));
            yield return Case("TaskLists.UncheckAll", ("text", text));
            yield return Case("TaskLists.RemoveChecked", ("text", text));
            yield return Case("TaskLists.ConvertMarkdownTasks", ("text", text));
            yield return Case("TaskLists.PendingTasks", ("text", text), ("max", 3));
            for (int i = 0; i < text.Length; i++)
            {
                yield return Case("TaskLists.SettleToggled", ("text", text), ("glyphIndex", i));
                yield return Case("TaskLines.ToggleCheckboxAt", ("text", text), ("index", i));
            }
        }

        // Título, vista previa y texto de la nota.
        string[] titles =
        [
            "Título", "  espacios  ", "# Tema 3", "## Tema\ncuerpo", "###### seis", "####### siete", "#hashtag", "#", "# ", "#   \nresto", "   \nsegunda",
            "a\r\nb\r\nc", "☐ a\n☒ b\n☐ c\nfin", "uno\n☐ a\n☐ b\n\n\n  texto   con   huecos  ", "t\n\t\n  \n", "😀 emoji\n😀 otra", "Título\r\n",
            "Título\n☒ solo hechas\n☒ otra", "x\n→ viñeta uno\n→ viñeta dos", "\t# con tab\nx", "#\ttab\nx"
        ];
        foreach (var text in titles)
        {
            yield return Case("NoteTitleHelper.GetTitle", ("text", text));
            yield return Case("NoteTitleHelper.GetPreview", ("text", text));
            yield return Case("NoteTitleHelper.GetTabPreview", ("text", text));
            yield return Case("NoteText.Split", ("text", text));
        }
        foreach (var (title, body) in new[]
                 {
                     ("", ""), ("t", ""), ("t", "b"), ("", "b"), ("t", "b1\r\nb2"), ("t", "b1\nb2"), ("t\r", "b"), ("☐ t", "☐ b")
                 })
            yield return Case("NoteText.Join", ("title", title), ("body", body));
    }

    // ───────────────────────── color ─────────────────────────

    /// <summary>Colores de partida: los de los temas de serie y los antiguos, extremos, grises, y una malla determinista.</summary>
    private static List<string> SampleColors()
    {
        var colors = new List<string>();
        colors.AddRange(NoteThemes.BuiltIn.SelectMany(t => t.DarkColors.Concat(t.LightColors)));
        colors.AddRange(NoteThemes.LegacySereneDarkColors);
        colors.AddRange(NoteThemes.LegacySereneLightColors);
        colors.AddRange(["#000000", "#FFFFFF", "#808080", "#777777", "#767676", "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#00FFFF",
            "#FF00FF", "#1E1A14", "#123456", "#abcdef", "#EBD38B", "#ebd38b", "#010101", "#FEFEFE", "#7F7F7F", "#FF8800"]);
        // Malla determinista (congruencial lineal): fija el fichero sin depender de un generador aleatorio.
        uint seed = 20261017;
        for (int i = 0; i < 72; i++)
        {
            seed = seed * 1664525u + 1013904223u;
            colors.Add($"#{(seed >> 8) & 0xFFFFFF:X6}");
        }
        return colors.Distinct(StringComparer.Ordinal).ToList();
    }

    private static readonly string?[] InvalidColors = [null, "", "#", "#12345", "#1234567", "123456", "#GGGGGG", "#12 456", " #112233", "#11223 ", "rgb(1,2,3)", "#１２３４５６"];

    private static IEnumerable<JsonObject> ColorCases()
    {
        var colors = SampleColors();
        var all = colors.Concat(InvalidColors.Where(c => c is not null)).ToList();

        foreach (var color in colors.Concat(InvalidColors!))
        {
            yield return Case("OklchColor.FromHex", ("hex", color));
            yield return Case("NoteColorContrast.Luminance", ("color", color));
            yield return Case("NoteColorContrast.ForegroundFor", ("background", color));
            yield return Case("NoteColorDerivation.IsDark", ("color", color));
            yield return Case("NoteColorDerivation.RestOutlineFor", ("color", color));
        }
        foreach (var color in all)
        {
            yield return Case("NoteColorDerivation.RimFor", ("color", color));
            yield return Case("NoteColorDerivation.LabelFor", ("color", color));
            yield return Case("NoteColorDerivation.StripeColor", ("color", color), ("ground", "#1E1E1E"));
            yield return Case("NoteColorDerivation.StripeColor", ("color", color), ("ground", "#FAFAFA"));
            yield return Case("NoteColorDerivation.StripeColor", ("color", color), ("ground", "no-es-color"));
        }
        yield return Case("NoteColorDerivation.ClassicColors");

        // Contraste entre pares: cada color frente a negro, blanco, la tinta, y los siguientes de la lista.
        for (int i = 0; i < colors.Count; i++)
        {
            foreach (var other in new[] { "#000000", "#FFFFFF", NoteColorContrast.Ink, colors[(i + 1) % colors.Count], colors[(i + 7) % colors.Count] })
                yield return Case("NoteColorContrast.IsReadable", ("background", colors[i]), ("foreground", other));
        }
        foreach (var invalid in new[] { null, "", "x" })
        {
            yield return Case("NoteColorContrast.IsReadable", ("background", invalid), ("foreground", "#000000"));
            yield return Case("NoteColorContrast.IsReadable", ("background", "#FFFFFF"), ("foreground", invalid));
        }

        // OKLCH → hex: dentro de gama, fuera de gama (se reduce el croma), extremos, matices fuera de 0-360 y croma 0.
        double[] lightness = [0, 0.05, 0.2, 0.31, 0.47, 0.6, 0.75, 0.86, 0.925, 0.98, 1];
        double[] chroma = [0, 0.02, 0.06, 0.12, 0.25, 0.4];
        double[] hues = [0, 30, 92, 148, 205, 268, 330, 359.9];
        foreach (var l in lightness)
            foreach (var c in chroma)
                foreach (var h in hues)
                    yield return Case("OklchColor.ToHex", ("l", l), ("c", c), ("h", h));
        foreach (var h in new[] { -90.0, 360.0, 450.0, 720.0 })
            yield return Case("OklchColor.ToHex", ("l", 0.7), ("c", 0.1), ("h", h));
        yield return Case("OklchColor.ToHex", ("l", 1.2), ("c", 0.1), ("h", 100.0));
        yield return Case("OklchColor.ToHex", ("l", -0.1), ("c", 0.1), ("h", 100.0));

        // Distancias OKLab.
        var pairs = NoteThemes.BuiltIn.SelectMany(t => t.DarkColors.Concat(t.LightColors)).Take(10).ToList();
        for (int i = 0; i + 1 < pairs.Count; i++)
        {
            OklchColor.TryFromHex(pairs[i], out var a);
            OklchColor.TryFromHex(pairs[i + 1], out var b);
            yield return Case("OklchColor.DistanceTo", ("l", a.L), ("c", a.C), ("h", a.H), ("l2", b.L), ("c2", b.C), ("h2", b.H));
        }
        yield return Case("OklchColor.DistanceTo", ("l", 0.5), ("c", 0.1), ("h", 10.0), ("l2", 0.5), ("c2", 0.1), ("h2", 10.0));
        yield return Case("OklchColor.DistanceTo", ("l", 0.5), ("c", 0.1), ("h", 350.0), ("l2", 0.5), ("c2", 0.1), ("h2", 10.0));

        // Temas.
        yield return Case("NoteThemes.BuiltIn");
        yield return Case("NoteThemes.Legacy");
        foreach (var id in new[] { "classic", "serene", "graphite", "gray", "xp", "pastel", "autumn", "ocean", "nope", "", "CLASSIC" })
            yield return Case("NoteThemes.Resolve", ("id", id));
        yield return Case("NoteThemes.Resolve", ("id", null));
        var mine = new JsonObject { ["id"] = "mio", ["name"] = "Mío", ["dark"] = Array(["#112233"]), ["light"] = Array(["#EEDDCC"]) };
        yield return Case("NoteThemes.Resolve", ("id", "mio"), ("custom", new JsonArray(mine.DeepClone())));
        yield return Case("NoteThemes.Resolve", ("id", "otro"), ("custom", new JsonArray(mine.DeepClone())));
        yield return Case("NoteThemes.Sanitize", ("custom", new JsonArray()));
        yield return Case("NoteThemes.Sanitize", ("custom", new JsonArray(
            mine.DeepClone(),
            new JsonObject { ["id"] = "mio", ["name"] = "repetido", ["dark"] = Array(["#000000"]), ["light"] = Array([]) },
            new JsonObject { ["id"] = "classic", ["name"] = "usurpa", ["dark"] = Array(["#000000"]), ["light"] = Array([]) },
            new JsonObject { ["id"] = " ", ["name"] = "sin id", ["dark"] = Array(["#000000"]), ["light"] = Array([]) },
            new JsonObject { ["id"] = "malo", ["name"] = "colores malos", ["dark"] = Array(["rojo", "#12345", "#abcdef"]), ["light"] = Array(["#GGGGGG"]) },
            new JsonObject { ["id"] = "vacio", ["name"] = "vacío", ["dark"] = Array(["nada"]), ["light"] = Array([]) },
            new JsonObject { ["id"] = "sinnombre", ["name"] = "  ", ["dark"] = Array([]), ["light"] = Array(["#ffffff", "#000000"]) },
            new JsonObject { ["id"] = "espacios", ["name"] = "  con espacios  ", ["dark"] = Array(["#0a0b0c"]), ["light"] = Array([]) })));

        // Color único de «mismo color en todas las notas».
        foreach (var uniform in new[] { null, "", "#abcdef", "#ABCDEF", "#12345", "rojo", "#GGGGGG" })
            yield return Case("NoteDisplayColor.Resolve", ("color", "#EBD38B"), ("uniform", uniform));

        // Reparto del color de una nota nueva: cada tema × tono × regla × estados del mazo.
        foreach (var theme in NoteThemes.BuiltIn)
        {
            var dark = theme.DarkColors; var light = theme.LightColors;
            var palette = dark.Concat(light).ToList();
            var other = NoteThemes.BuiltIn.First(t => t.Id != theme.Id).LightColors;
            var docks = new List<List<string>>
            {
                new(),
                new() { palette[0] },
                new() { palette[0], palette[Math.Min(1, palette.Count - 1)] },
                palette.Take(4).Reverse().ToList(),
                palette.ToList(),
                new() { palette[^1], palette[0], palette[^1] },
                new() { other[0], other[^1] },
                new() { palette[0].ToLowerInvariant() },
                new() { "#123456", "no-es-color" },
            };
            var fixeds = new string?[] { null, palette[0], palette[^1].ToLowerInvariant(), "#123456" };
            for (int tone = 0; tone < 3; tone++)
                for (int rule = 0; rule < 4; rule++)
                    foreach (var dock in docks)
                        foreach (var fixedColor in rule == 3 ? fixeds : [null])
                            yield return Case("NoteColorAssigner.Assign", ("themeId", theme.Id), ("tone", tone), ("rule", rule),
                                ("fixedColor", fixedColor), ("dockColors", Array(dock)));
        }
        // Temas propios raros: sin un tono, con un solo color, y sin ninguno (cae en Clásico).
        var oneTone = new JsonObject { ["id"] = "solo-claro", ["name"] = "solo claro", ["dark"] = Array([]), ["light"] = Array(["#FFDDD4", "#EFE7C7"]) };
        var single = new JsonObject { ["id"] = "uno", ["name"] = "uno", ["dark"] = Array(["#472525"]), ["light"] = Array([]) };
        var empty = new JsonObject { ["id"] = "nada", ["name"] = "nada", ["dark"] = Array([]), ["light"] = Array([]) };
        foreach (var theme in new[] { oneTone, single, empty })
            for (int tone = 0; tone < 3; tone++)
                for (int rule = 0; rule < 4; rule++)
                    foreach (var dock in new List<string>[] { [], ["#472525"], ["#FFDDD4", "#EFE7C7"] })
                        yield return Case("NoteColorAssigner.Assign", ("theme", theme.DeepClone()), ("tone", tone), ("rule", rule),
                            ("fixedColor", rule == 3 ? "#EFE7C7" : null), ("dockColors", Array(dock)));
    }

    // ───────────────────────── orden ─────────────────────────

    private static IEnumerable<JsonObject> OrderCases()
    {
        double?[] bounds = [null, 0, 1000, 2000, -1024, 1024, 0.5, 1.0000000000000002, 5e-324, 1e15, 1e16, -1e16, 123.456];
        foreach (var before in bounds)
            foreach (var after in bounds)
                yield return Case("NoteOrdering.Between", ("before", before), ("after", after));
        // Dos vecinas que ya no admiten un número entre ellas (precisión del double).
        yield return Case("NoteOrdering.Between", ("before", 1.0), ("after", 1.0000000000000002));
        yield return Case("NoteOrdering.Between", ("before", 5.0), ("after", 5.0));
        yield return Case("NoteOrdering.Between", ("before", 5.0), ("after", 4.0));
        foreach (var count in new[] { -2, 0, 1, 2, 5, 40 })
            yield return Case("NoteOrdering.Spaced", ("count", count));
        foreach (var delta in new[] { 0.0, 5, 13, 19.4, 19.5, 20, 26, 38.9, 39, 40, 52, 78, -5, -19.5, -20, -26, -39, -78, 1000, -1000 })
        {
            yield return Case("NoteOrdering.SlotShift", ("delta", delta), ("pitch", 26.0));
            yield return Case("NoteOrdering.SlotShift", ("delta", delta), ("pitch", 26.0), ("hysteresis", 0.5));
            yield return Case("NoteOrdering.SlotShift", ("delta", delta), ("pitch", 0.0));
            yield return Case("NoteOrdering.SlotShift", ("delta", delta), ("pitch", -26.0));
            foreach (var original in new[] { 0, 2, 5 })
                foreach (var count in new[] { 0, 1, 6 })
                    yield return Case("NoteOrdering.TargetIndex", ("originalIndex", original), ("delta", delta), ("pitch", 26.0), ("count", count));
        }
    }
}
