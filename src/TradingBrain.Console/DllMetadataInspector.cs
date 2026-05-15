using System.Text;
using Mono.Cecil;

namespace TradingBrain.ConsoleApp;

public static class DllMetadataInspector
{
    public static void Inspect(string assemblyPath, string reportPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("DLL nao encontrada.", assemblyPath);
        }

        var resolver = new DefaultAssemblyResolver();
        var assemblyDir = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            resolver.AddSearchDirectory(assemblyDir);
        }

        AddIfExists(resolver, @"C:\Program Files\NinjaTrader 8\bin");
        AddIfExists(resolver, @"C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\bin\Custom");

        var readerParameters = new ReaderParameters
        {
            ReadSymbols = false,
            AssemblyResolver = resolver,
            InMemory = true
        };

        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
        var report = BuildReport(assemblyPath, assembly);
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(reportPath, report, Encoding.UTF8);
    }

    private static string BuildReport(string assemblyPath, AssemblyDefinition assembly)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# DLL Metadata Inspection");
        sb.AppendLine();
        sb.AppendLine($"- Assembly: `{assembly.Name.Name}`");
        sb.AppendLine($"- Path: `{assemblyPath}`");
        sb.AppendLine($"- Generated: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine("- Mode: static metadata read only; no execution, no patching, no instantiation.");
        sb.AppendLine();

        AppendAssemblyReferences(sb, assembly);
        AppendModuleCctor(sb, assembly);
        AppendStrategies(sb, assembly);
        AppendCandidateIndicators(sb, assembly);
        return sb.ToString();
    }

    private static void AppendAssemblyReferences(StringBuilder sb, AssemblyDefinition assembly)
    {
        sb.AppendLine("## Assembly References");
        sb.AppendLine();
        foreach (var reference in assembly.MainModule.AssemblyReferences.OrderBy(r => r.Name))
        {
            sb.AppendLine($"- `{reference.Name}` `{reference.Version}`");
        }

        sb.AppendLine();
    }

    private static void AppendModuleCctor(StringBuilder sb, AssemblyDefinition assembly)
    {
        sb.AppendLine("## Module Initializer");
        sb.AppendLine();
        var moduleType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "<Module>");
        var cctor = moduleType?.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
        if (cctor == null)
        {
            sb.AppendLine("- `<Module>.cctor`: not found");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"- `<Module>.cctor`: found");
        sb.AppendLine($"- Has body: `{cctor.HasBody}`");
        sb.AppendLine($"- IL instructions: `{(cctor.HasBody ? cctor.Body.Instructions.Count : 0)}`");
        sb.AppendLine();
    }

    private static void AppendStrategies(StringBuilder sb, AssemblyDefinition assembly)
    {
        var strategyTypes = assembly.MainModule.Types
            .SelectMany(Flatten)
            .Where(IsStrategyType)
            .OrderBy(t => t.FullName)
            .ToList();

        sb.AppendLine($"## Strategy Types ({strategyTypes.Count})");
        sb.AppendLine();

        foreach (var type in strategyTypes)
        {
            sb.AppendLine($"### `{type.FullName}`");
            sb.AppendLine();
            sb.AppendLine($"- Base: `{type.BaseType?.FullName ?? ""}`");
            sb.AppendLine($"- Token: `{FormatToken(type.MetadataToken)}`");
            sb.AppendLine();

            sb.AppendLine("#### Fields");
            foreach (var field in type.Fields.Where(f => !f.IsStatic).OrderBy(f => f.MetadataToken.ToInt32()))
            {
                sb.AppendLine($"- `{FormatToken(field.MetadataToken)}` `{field.FieldType.FullName}` `{field.Name}`");
            }

            if (!type.Fields.Any(f => !f.IsStatic))
            {
                sb.AppendLine("- none");
            }

            sb.AppendLine();
            sb.AppendLine("#### Properties");
            foreach (var property in type.Properties.OrderBy(p => p.Name))
            {
                sb.AppendLine($"- `{property.PropertyType.FullName}` `{property.Name}`");
            }

            if (!type.Properties.Any())
            {
                sb.AppendLine("- none");
            }

            sb.AppendLine();
            AppendMethodSummary(sb, type, "OnStateChange");
            AppendMethodSummary(sb, type, "OnBarUpdate");
            AppendTradingCalls(sb, type);
            sb.AppendLine();
        }
    }

    private static void AppendMethodSummary(StringBuilder sb, TypeDefinition type, string methodName)
    {
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        sb.AppendLine($"#### `{methodName}`");
        if (method == null)
        {
            sb.AppendLine("- not found");
            sb.AppendLine();
            return;
        }

        var instructionCount = method.HasBody ? method.Body.Instructions.Count : 0;
        var onlyRet = method.HasBody &&
            instructionCount == 1 &&
            method.Body.Instructions[0].OpCode.Code == Mono.Cecil.Cil.Code.Ret;

        sb.AppendLine($"- Token: `{FormatToken(method.MetadataToken)}`");
        sb.AppendLine($"- Has body: `{method.HasBody}`");
        sb.AppendLine($"- IL instructions: `{instructionCount}`");
        sb.AppendLine($"- Ret-only body: `{onlyRet}`");
        sb.AppendLine();
    }

    private static void AppendTradingCalls(StringBuilder sb, TypeDefinition type)
    {
        var interesting = new[]
        {
            "EnterLong", "EnterShort", "ExitLong", "ExitShort",
            "SetStopLoss", "SetProfitTarget", "SetTrailStop"
        };

        var calls = type.Methods
            .Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions
                .Select(i => i.Operand as MethodReference)
                .Where(mr => mr != null && interesting.Any(name => mr!.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .Select(mr => $"`{m.Name}` -> `{mr!.DeclaringType.FullName}.{mr.Name}`"))
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        sb.AppendLine("#### Recognized Trading Calls");
        if (calls.Count == 0)
        {
            sb.AppendLine("- none found");
            return;
        }

        foreach (var call in calls)
        {
            sb.AppendLine($"- {call}");
        }
    }

    private static void AppendCandidateIndicators(StringBuilder sb, AssemblyDefinition assembly)
    {
        var indicatorFields = assembly.MainModule.Types
            .SelectMany(Flatten)
            .SelectMany(t => t.Fields.Select(f => new { Type = t, Field = f }))
            .Where(x => x.Field.FieldType.FullName.Contains("NinjaTrader.NinjaScript.Indicators.", StringComparison.Ordinal))
            .OrderBy(x => x.Type.FullName)
            .ThenBy(x => x.Field.Name)
            .ToList();

        sb.AppendLine("## Indicator Fields");
        sb.AppendLine();
        if (indicatorFields.Count == 0)
        {
            sb.AppendLine("- none");
            sb.AppendLine();
            return;
        }

        foreach (var item in indicatorFields)
        {
            sb.AppendLine($"- `{item.Type.FullName}`: `{item.Field.FieldType.FullName}` `{item.Field.Name}`");
        }

        sb.AppendLine();
    }

    private static bool IsStrategyType(TypeDefinition type)
    {
        var baseType = type.BaseType?.FullName ?? "";
        return baseType.Equals("NinjaTrader.NinjaScript.Strategies.Strategy", StringComparison.Ordinal) ||
               baseType.EndsWith(".Strategies.Strategy", StringComparison.Ordinal);
    }

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes.SelectMany(Flatten))
        {
            yield return nested;
        }
    }

    private static void AddIfExists(DefaultAssemblyResolver resolver, string path)
    {
        if (Directory.Exists(path))
        {
            resolver.AddSearchDirectory(path);
        }
    }

    private static string FormatToken(MetadataToken token)
    {
        return $"0x{token.ToInt32():X8}";
    }
}
