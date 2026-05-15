using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Menus;
using Microsoft.VisualBasic;

namespace VendorNotificationAuditPlugin;

// Cole este arquivo no projeto atual do plugin.
// Ele usa AuditMenuConstants e ModuleFinder que ja existem em VendorNotificationAuditPlugin.cs.
[ExportMenuItem(
    OwnerGuid = AuditMenuConstants.MenuGuid,
    Header = "F - Static Strategy Flow Mapper (Read-Only)",
    Group = AuditMenuConstants.GroupGuid,
    Order = 6)]
public sealed class StaticStrategyFlowMapperCommand : MenuItemBase {
    private readonly IDsDocumentService documentService;

    [ImportingConstructor]
    public StaticStrategyFlowMapperCommand(IDsDocumentService documentService) {
        this.documentService = documentService;
    }

    public override void Execute(IMenuItemContext context) {
        try {
            var modules = ModuleFinder.FindModuleDefs(documentService)
                .GroupBy(m => m.Location ?? m.Name)
                .Select(g => g.First())
                .OrderBy(m => Path.GetFileName(m.Location ?? m.Name), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (modules.Count == 0) {
                MsgBox.Instance.Show("Nenhum modulo carregado no dnSpy.");
                return;
            }

            var module = SelectModule(modules);
            if (module == null)
                return;

            var report = BuildReport(module);
            ShowReport(report);
        }
        catch (Exception ex) {
            MsgBox.Instance.Show("Falha no Static Strategy Flow Mapper:" + Environment.NewLine + Environment.NewLine + ex);
        }
    }

    private static ModuleDef? SelectModule(IReadOnlyList<ModuleDef> modules) {
        var defaultModule = modules.FirstOrDefault(m =>
            Path.GetFileName(m.Location ?? m.Name).Equals("NinjaBotIA.dll", StringComparison.OrdinalIgnoreCase)) ??
            modules.First();

        var prompt = "Digite o nome do modulo da strategy carregado no dnSpy." + Environment.NewLine +
                     "Exemplo: NinjaBotIA.dll" + Environment.NewLine +
                     Environment.NewLine +
                     "Modulos disponiveis:" + Environment.NewLine +
                     string.Join(Environment.NewLine, modules.Select(DescribeModule));

        var moduleText = Interaction.InputBox(
            prompt,
            "Static Strategy Flow Mapper",
            Path.GetFileName(defaultModule.Location ?? defaultModule.Name));

        if (string.IsNullOrWhiteSpace(moduleText))
            return null;

        var selected = modules.FirstOrDefault(m => ModuleMatches(m, moduleText.Trim()));
        if (selected == null) {
            MsgBox.Instance.Show("Modulo nao encontrado: " + moduleText);
            return null;
        }

        return selected;
    }

    private static string BuildReport(ModuleDef module) {
        var report = new StringBuilder();
        report.AppendLine("=== STATIC STRATEGY FLOW MAPPER ===");
        report.AppendLine("Modulo: " + DescribeModule(module));
        report.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        report.AppendLine();
        report.AppendLine("Modo: leitura estatica de metadados/IL. Nenhum metodo e executado e nenhum IL e alterado.");
        report.AppendLine();

        var strategies = FindStrategyTypes(module).ToList();
        report.AppendLine("[STRATEGY TYPES: " + strategies.Count + "]");
        report.AppendLine(new string('-', 100));

        if (strategies.Count == 0) {
            report.AppendLine("Nenhuma classe que herda de Strategy foi encontrada por heuristica.");
            report.AppendLine("Dica: procure manualmente por OnBarUpdate/OnStateChange; a heranca pode estar indireta ou ofuscada.");
            AppendMethodsNamedLikeStrategyLifecycle(report, module);
            return report.ToString();
        }

        foreach (var strategy in strategies) {
            AppendStrategyReport(report, strategy);
            report.AppendLine();
        }

        return report.ToString();
    }

    private static IEnumerable<TypeDef> FindStrategyTypes(ModuleDef module) {
        foreach (var type in module.GetTypes()) {
            if (InheritsFromStrategy(type))
                yield return type;
        }
    }

    private static bool InheritsFromStrategy(TypeDef type) {
        var current = type;
        var guard = 0;

        while (current != null && guard++ < 20) {
            var baseType = current.BaseType;
            if (baseType == null)
                return false;

            if (baseType.FullName == "NinjaTrader.NinjaScript.Strategies.Strategy" ||
                baseType.FullName.EndsWith(".Strategy", StringComparison.OrdinalIgnoreCase) ||
                baseType.Name == "Strategy")
                return true;

            try {
                current = baseType.ResolveTypeDef();
            }
            catch {
                return false;
            }
        }

        return false;
    }

    private static void AppendStrategyReport(StringBuilder report, TypeDef strategy) {
        report.AppendLine();
        report.AppendLine("Strategy: " + strategy.FullName);
        report.AppendLine("Base: " + (strategy.BaseType?.FullName ?? "<sem base>"));
        report.AppendLine("Token: 0x" + strategy.MDToken.Raw.ToString("X8"));
        report.AppendLine();

        var fields = strategy.Fields.Where(f => !f.IsLiteral).ToList();
        report.AppendLine("[FIELDS: " + fields.Count + "]");
        foreach (var field in fields)
            report.AppendLine("  0x" + field.MDToken.Raw.ToString("X8") + " " + field.FieldType.FullName + " " + field.Name);
        report.AppendLine();

        var onStateChange = FindMethod(strategy, "OnStateChange");
        var onBarUpdate = FindMethod(strategy, "OnBarUpdate");

        AppendLifecycleMethod(report, "OnStateChange", onStateChange);
        AppendIndicatorAssignments(report, onStateChange);
        AppendLifecycleMethod(report, "OnBarUpdate", onBarUpdate);
        AppendTradingCalls(report, onBarUpdate);
        AppendBranchSummary(report, onBarUpdate);
        AppendExternalCalls(report, onBarUpdate);
    }

    private static MethodDef? FindMethod(TypeDef type, string name) =>
        type.Methods.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));

    private static void AppendLifecycleMethod(StringBuilder report, string label, MethodDef? method) {
        report.AppendLine("[" + label + "]");
        report.AppendLine(new string('-', 100));

        if (method == null) {
            report.AppendLine("  Metodo nao encontrado.");
            report.AppendLine();
            return;
        }

        report.AppendLine("  Metodo: " + method.FullName);
        report.AppendLine("  Token: 0x" + method.MDToken.Raw.ToString("X8"));
        report.AppendLine("  HasBody: " + method.HasBody);
        report.AppendLine("  IL instructions: " + (method.Body?.Instructions.Count ?? 0));
        report.AppendLine();
    }

    private static void AppendIndicatorAssignments(StringBuilder report, MethodDef? onStateChange) {
        report.AppendLine("[INDICATOR/FIELD MAP FROM OnStateChange]");
        report.AppendLine(new string('-', 100));

        if (onStateChange?.Body == null) {
            report.AppendLine("  Sem corpo IL para analisar.");
            report.AppendLine();
            return;
        }

        var hits = new List<string>();
        var instructions = onStateChange.Body.Instructions;

        for (var i = 0; i < instructions.Count; i++) {
            var instr = instructions[i];
            if (instr.OpCode.Code != Code.Stfld || instr.Operand is not FieldDef field)
                continue;

            var call = FindPreviousCall(instructions, i);
            var constants = FindNearbyConstants(instructions, i, lookback: 10);
            hits.Add("  " + field.Name + " <= " + FormatCall(call) + FormatConstants(constants));
        }

        if (hits.Count == 0)
            report.AppendLine("  Nenhuma atribuicao stfld com chamada anterior encontrada.");
        else
            foreach (var hit in hits.Distinct())
                report.AppendLine(hit);

        report.AppendLine();
    }

    private static void AppendTradingCalls(StringBuilder report, MethodDef? onBarUpdate) {
        report.AppendLine("[TRADING ACTION CALLS IN OnBarUpdate]");
        report.AppendLine(new string('-', 100));

        if (onBarUpdate?.Body == null) {
            report.AppendLine("  Sem corpo IL para analisar.");
            report.AppendLine();
            return;
        }

        var tradingPrefixes = new[] {
            "EnterLong", "EnterShort", "ExitLong", "ExitShort",
            "SetStopLoss", "SetProfitTarget", "SetTrailStop",
            "SubmitOrder", "SubmitOrderUnmanaged", "ChangeOrder", "CancelOrder"
        };

        var hits = onBarUpdate.Body.Instructions
            .Where(i => IsCall(i) && i.Operand is IMethod method && tradingPrefixes.Any(p => method.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Select(i => "  IL_" + i.Offset.ToString("X4") + " " + ((IMethod)i.Operand).FullName)
            .ToList();

        if (hits.Count == 0)
            report.AppendLine("  Nenhuma chamada de ordem reconhecida.");
        else
            foreach (var hit in hits)
                report.AppendLine(hit);

        report.AppendLine();
    }

    private static void AppendBranchSummary(StringBuilder report, MethodDef? onBarUpdate) {
        report.AppendLine("[CONDITIONAL BRANCHES IN OnBarUpdate]");
        report.AppendLine(new string('-', 100));

        if (onBarUpdate?.Body == null) {
            report.AppendLine("  Sem corpo IL para analisar.");
            report.AppendLine();
            return;
        }

        var branches = onBarUpdate.Body.Instructions
            .Where(i => i.OpCode.FlowControl == FlowControl.Cond_Branch)
            .ToList();

        report.AppendLine("  Total conditional branches: " + branches.Count);
        foreach (var branch in branches.Take(80))
            report.AppendLine("  IL_" + branch.Offset.ToString("X4") + " " + branch.OpCode.Name + " -> " + FormatOperand(branch.Operand));

        if (branches.Count > 80)
            report.AppendLine("  ... " + (branches.Count - 80) + " branches adicionais omitidos.");

        report.AppendLine();
    }

    private static void AppendExternalCalls(StringBuilder report, MethodDef? onBarUpdate) {
        report.AppendLine("[CALLS/INDICATORS IN OnBarUpdate]");
        report.AppendLine(new string('-', 100));

        if (onBarUpdate?.Body == null) {
            report.AppendLine("  Sem corpo IL para analisar.");
            report.AppendLine();
            return;
        }

        var calls = onBarUpdate.Body.Instructions
            .Where(IsCall)
            .Select(i => i.Operand as IMethod)
            .Where(m => m != null)
            .Cast<IMethod>()
            .GroupBy(m => m.FullName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var call in calls)
            report.AppendLine("  x" + call.Count().ToString(CultureInfo.InvariantCulture).PadLeft(2) + " " + call.Key);

        if (calls.Count == 0)
            report.AppendLine("  Nenhuma chamada encontrada.");

        report.AppendLine();
    }

    private static void AppendMethodsNamedLikeStrategyLifecycle(StringBuilder report, ModuleDef module) {
        report.AppendLine();
        report.AppendLine("[LIFECYCLE-LIKE METHODS]");
        report.AppendLine(new string('-', 100));

        var methods = module.GetTypes()
            .SelectMany(t => t.Methods)
            .Where(m => m.Name.String.Contains("OnBar", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.String.Contains("OnState", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.String.Contains("Update", StringComparison.OrdinalIgnoreCase))
            .Take(100)
            .ToList();

        foreach (var method in methods)
            report.AppendLine("  0x" + method.MDToken.Raw.ToString("X8") + " " + method.FullName);
    }

    private static IMethod? FindPreviousCall(IList<Instruction> instructions, int startIndex) {
        for (var i = startIndex - 1; i >= 0 && i >= startIndex - 12; i--) {
            if (IsCall(instructions[i]) && instructions[i].Operand is IMethod method)
                return method;
        }

        return null;
    }

    private static IReadOnlyList<string> FindNearbyConstants(IList<Instruction> instructions, int startIndex, int lookback) {
        var values = new List<string>();
        for (var i = Math.Max(0, startIndex - lookback); i < startIndex; i++) {
            var value = TryReadConstant(instructions[i]);
            if (value != null)
                values.Add(value);
        }

        return values;
    }

    private static string? TryReadConstant(Instruction instr) {
        return instr.OpCode.Code switch {
            Code.Ldc_I4_M1 => "-1",
            Code.Ldc_I4_0 => "0",
            Code.Ldc_I4_1 => "1",
            Code.Ldc_I4_2 => "2",
            Code.Ldc_I4_3 => "3",
            Code.Ldc_I4_4 => "4",
            Code.Ldc_I4_5 => "5",
            Code.Ldc_I4_6 => "6",
            Code.Ldc_I4_7 => "7",
            Code.Ldc_I4_8 => "8",
            Code.Ldc_I4 or Code.Ldc_I4_S => Convert.ToString(instr.Operand, CultureInfo.InvariantCulture),
            Code.Ldc_R4 or Code.Ldc_R8 => Convert.ToString(instr.Operand, CultureInfo.InvariantCulture),
            Code.Ldstr => "\"" + Convert.ToString(instr.Operand, CultureInfo.InvariantCulture) + "\"",
            _ => null
        };
    }

    private static bool IsCall(Instruction instruction) =>
        instruction.OpCode.Code == Code.Call ||
        instruction.OpCode.Code == Code.Callvirt ||
        instruction.OpCode.Code == Code.Newobj;

    private static string FormatCall(IMethod? method) =>
        method == null ? "<chamada nao identificada>" : method.FullName;

    private static string FormatConstants(IReadOnlyList<string> constants) =>
        constants.Count == 0 ? "" : " | nearby constants: " + string.Join(", ", constants);

    private static string FormatOperand(object? operand) {
        if (operand == null)
            return "";
        if (operand is Instruction instruction)
            return "IL_" + instruction.Offset.ToString("X4");
        if (operand is IMemberDef memberDef)
            return memberDef.FullName;
        if (operand is IMethod method)
            return method.FullName;
        if (operand is IField field)
            return field.FullName;
        return operand.ToString() ?? "";
    }

    private static bool ModuleMatches(ModuleDef module, string text) {
        var moduleName = module.Name;
        var fileName = Path.GetFileName(module.Location ?? "");

        return string.Equals(moduleName, text, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, text, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(module.Location, text, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeModule(ModuleDef module) {
        var name = Path.GetFileName(module.Location ?? "");
        if (string.IsNullOrWhiteSpace(name))
            name = module.Name;

        return string.IsNullOrWhiteSpace(module.Location)
            ? name
            : name + " (" + module.Location + ")";
    }

    private static void ShowReport(string report) {
        var window = new Window {
            Title = "Static Strategy Flow Mapper (Read-Only)",
            Width = 1100,
            Height = 820,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };

        var panel = new DockPanel { Margin = new Thickness(12) };
        var buttonPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var copyButton = new Button {
            Content = "Copiar relatorio",
            MinWidth = 120,
            Height = 28,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var saveButton = new Button {
            Content = "Salvar em arquivo",
            MinWidth = 120,
            Height = 28
        };

        buttonPanel.Children.Add(copyButton);
        buttonPanel.Children.Add(saveButton);
        DockPanel.SetDock(buttonPanel, Dock.Bottom);

        var textBox = new TextBox {
            Text = report,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11
        };

        copyButton.Click += (_, _) => {
            Clipboard.SetText(report);
            MsgBox.Instance.Show("Relatorio copiado para o clipboard.");
        };

        saveButton.Click += (_, _) => {
            var path = Path.Combine(Path.GetTempPath(), "strategy_flow_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
            File.WriteAllText(path, report, Encoding.UTF8);
            MsgBox.Instance.Show("Relatorio salvo em:" + Environment.NewLine + path);
        };

        panel.Children.Add(buttonPanel);
        panel.Children.Add(textBox);
        window.Content = panel;
        window.ShowDialog();
    }
}
