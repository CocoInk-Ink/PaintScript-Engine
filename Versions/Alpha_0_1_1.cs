using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PaintScript_Engine.Versions;

public static class PaintScriptEngine_Alpha_0_1_1
{
    public static PaintScriptEngine_Alpha_0_1_0.PSProgram LoadProgram(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("PaintScript JSON cannot be empty.");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("targets", out _))
        {
            var program = JsonSerializer.Deserialize<PaintScriptEngine_Alpha_0_1_0.PSProgram>(json, options);
            if (program != null)
                return program;
        }

        var programModel = new PaintScriptEngine_Alpha_0_1_0.PSProgram
        {
            Version = GetString(root, "version")
        };

        if (root.TryGetProperty("globals", out var globalsElement))
        {
            if (globalsElement.TryGetProperty("variables", out var globalsVariables))
                programModel.Globals = ParseVariables<PaintScriptEngine_Alpha_0_1_0.PSGlobalVariable>(globalsVariables, options);

            if (globalsElement.TryGetProperty("functions", out var globalsFunctions))
                programModel.GlobalFunctions = ParseFunctions(globalsFunctions, options);
        }

        var target = new PaintScriptEngine_Alpha_0_1_0.PSTarget
        {
            Name = GetString(root, "name") ?? "Sprite",
            Instance = GetString(root, "instance") ?? GetString(root, "name") ?? "Sprite"
        };

        if (root.TryGetProperty("variables", out var variablesElement))
            target.Variables = ParseVariables<PaintScriptEngine_Alpha_0_1_0.PSVariable>(variablesElement, options);

        if (root.TryGetProperty("functions", out var functionsElement))
            target.Functions = ParseFunctions(functionsElement, options);

        if (root.TryGetProperty("events", out var eventsElement))
            target.Events = ParseEvents(eventsElement, options);

        programModel.Targets.Add(target);
        NormalizeProgram(programModel);
        return programModel;
    }

    public static void StartTargetEvents(PaintScriptEngine_Alpha_0_1_0.PaintScriptEngine engine, PaintScriptEngine_Alpha_0_1_0.PSProgram program)
    {
        foreach (var target in program.Targets)
        {
            foreach (var eventName in target.Events.Keys)
            {
                var normalized = eventName.TrimStart('@');
                if (string.Equals(normalized, "Start", StringComparison.OrdinalIgnoreCase))
                {
                    engine.StartEvent(target, eventName);
                    break;
                }
            }
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static Dictionary<string, T> ParseVariables<T>(JsonElement element, JsonSerializerOptions options)
        where T : PaintScriptEngine_Alpha_0_1_0.PSVariable, new()
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        if (element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in element.EnumerateObject())
        {
            var raw = property.Value;
            T? entry = JsonSerializer.Deserialize<T>(raw.GetRawText(), options);
            if (entry == null)
                continue;

            if (string.IsNullOrWhiteSpace(entry.Name))
                entry.Name = property.Name;

            result[property.Name] = entry;
        }

        return result;
    }

    private static Dictionary<string, PaintScriptEngine_Alpha_0_1_0.PSFunction> ParseFunctions(JsonElement element, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, PaintScriptEngine_Alpha_0_1_0.PSFunction>(StringComparer.OrdinalIgnoreCase);

        if (element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in element.EnumerateObject())
        {
            var raw = property.Value;
            var function = new PaintScriptEngine_Alpha_0_1_0.PSFunction
            {
                Name = raw.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString() ?? property.Name
                    : property.Name,
                Strict = raw.TryGetProperty("strict", out var strictElement) && strictElement.ValueKind == JsonValueKind.True,
                ReturnType = raw.TryGetProperty("returnType", out var returnTypeElement) && returnTypeElement.ValueKind == JsonValueKind.String
                    ? returnTypeElement.GetString() ?? "*"
                    : "*",
                IsPublic = raw.TryGetProperty("isPublic", out var isPublicElement) && isPublicElement.ValueKind == JsonValueKind.True,
                IsPrivate = raw.TryGetProperty("isPrivate", out var isPrivateElement) && isPrivateElement.ValueKind == JsonValueKind.True,
                Parameters = new List<PaintScriptEngine_Alpha_0_1_0.PSParameter>(),
                Code = new List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>()
            };

            if (raw.TryGetProperty("parameters", out var parametersElement))
            {
                foreach (var parameter in parametersElement.EnumerateArray())
                {
                    if (parameter.ValueKind == JsonValueKind.String)
                    {
                        function.Parameters.Add(new PaintScriptEngine_Alpha_0_1_0.PSParameter
                        {
                            Name = parameter.GetString() ?? "",
                            Type = "*"
                        });
                    }
                    else if (parameter.ValueKind == JsonValueKind.Object)
                    {
                        var name = parameter.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                            ? nameProp.GetString() ?? ""
                            : "";
                        var type = parameter.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
                            ? typeProp.GetString() ?? "*"
                            : "*";

                        function.Parameters.Add(new PaintScriptEngine_Alpha_0_1_0.PSParameter
                        {
                            Name = name,
                            Type = type
                        });
                    }
                }
            }

            if (raw.TryGetProperty("code", out var codeElement))
            {
                function.Code = JsonSerializer.Deserialize<List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>>(codeElement.GetRawText(), options) ?? new List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>();
            }

            result[property.Name] = function;
        }

        return result;
    }

    private static Dictionary<string, List<List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>>> ParseEvents(JsonElement element, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, List<List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>>>(StringComparer.OrdinalIgnoreCase);

        if (element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in element.EnumerateObject())
        {
            var handlers = new List<List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>>();

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var handler in property.Value.EnumerateArray())
                {
                    if (handler.ValueKind == JsonValueKind.Array)
                    {
                        handlers.Add(JsonSerializer.Deserialize<List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>>(handler.GetRawText(), options) ?? new List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>());
                    }
                    else if (handler.ValueKind == JsonValueKind.Object)
                    {
                        var one = JsonSerializer.Deserialize<PaintScriptEngine_Alpha_0_1_0.PSInstruction>(handler.GetRawText(), options);
                        if (one != null)
                            handlers.Add(new List<PaintScriptEngine_Alpha_0_1_0.PSInstruction> { one });
                    }
                }
            }

            result[property.Name] = handlers;
        }

        return result;
    }

    private static void NormalizeProgram(PaintScriptEngine_Alpha_0_1_0.PSProgram program)
    {
        foreach (var target in program.Targets)
        {
            var normalizedEvents = new Dictionary<string, List<List<PaintScriptEngine_Alpha_0_1_0.PSInstruction>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in target.Events)
            {
                var key = entry.Key.TrimStart('@');
                normalizedEvents[key] = entry.Value;
            }

            target.Events = normalizedEvents;
        }
    }
}
