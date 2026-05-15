using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;

var ninjaBin = @"C:\Program Files\NinjaTrader 8\bin";
var customBin = @"C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\bin\Custom";
var targetPath = Path.Combine(customBin, "NinjaBotIA.dll");
var reportPath = Path.Combine(Environment.CurrentDirectory, "locked-dll-playground-probe-report.txt");

AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
{
    var name = new AssemblyName(args.Name).Name + ".dll";
    foreach (var dir in new[] { customBin, ninjaBin })
    {
        var path = Path.Combine(dir, name);
        if (File.Exists(path))
        {
            return Assembly.LoadFrom(path);
        }
    }

    return null;
};

var targetTypes = new[]
{
    "NinjaTrader.NinjaScript.Strategies.ema",
    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAGoldBreakout_v1_0_0_0",
    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAMomentum_v1_0_0_0",
    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIARange_v1_0_0_0",
    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIATrend_v1_0_0_1",
    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0"
};

var report = new StringBuilder();
Line("=== LOCKED DLL PLAYGROUND PROBE ===");
Line("Target: " + targetPath);
Line("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
Line();
DumpEnvironment(targetPath);
Line();

try
{
    var assembly = Assembly.LoadFrom(targetPath);
    Line("Assembly loaded: " + assembly.FullName);
    Line();

    foreach (var typeName in targetTypes)
    {
        ProbeType(assembly, typeName);
    }
}
catch (Exception ex)
{
    Line("FATAL: " + ex);
    PrintExceptionChain(ex, 1);
}

File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
Console.WriteLine(report.ToString());
Console.WriteLine("Report: " + reportPath);

void ProbeType(Assembly assembly, string typeName)
{
    Line("TYPE " + typeName);
    var type = assembly.GetType(typeName, throwOnError: false);
    if (type == null)
    {
        Line("  not found");
        Line();
        return;
    }

    Try("force .cctor", () => RuntimeHelpers.RunClassConstructor(type.TypeHandle));

    object? instance = null;
    Try("create instance", () => instance = Activator.CreateInstance(type));

    if (instance == null)
    {
        Line("  instance unavailable; skipping method calls");
        Line();
        return;
    }

    PrintSimpleProperties(type, instance);
    PrintSimpleFields(type, instance, "before");

    TryCallLifecycle(type, instance, "OnStateChange");
    TryCallLifecycle(type, instance, "OnBarUpdate");

    PrintSimpleFields(type, instance, "after");
    Line();
}

void TryCallLifecycle(Type type, object instance, string methodName)
{
    var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (method == null)
    {
        Line("  " + methodName + ": not found");
        return;
    }

    var bodyLength = method.GetMethodBody()?.GetILAsByteArray()?.Length;
    Line("  " + methodName + " IL length: " + (bodyLength?.ToString() ?? "<none>"));
    Try("call " + methodName, () => method.Invoke(instance, null));
}

void PrintSimpleProperties(Type type, object instance)
{
    var properties = type
        .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(p => IsSimple(p.PropertyType))
        .OrderBy(p => p.Name);

    Line("  PROPERTIES");
    foreach (var property in properties)
    {
        var getter = property.GetGetMethod(nonPublic: true);
        if (getter == null)
        {
            Line("    " + property.Name + " = <no getter>");
            continue;
        }

        try
        {
            Line("    " + property.Name + " = " + Format(getter.Invoke(instance, null)));
        }
        catch (Exception ex)
        {
            Line("    " + property.Name + " = <failed " + ex.GetType().Name + ">");
        }
    }
}

void PrintSimpleFields(Type type, object instance, string label)
{
    var fields = type
        .GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => IsSimple(f.FieldType))
        .OrderBy(f => f.Name);

    Line("  SIMPLE FIELDS " + label);
    foreach (var field in fields)
    {
        try
        {
            Line("    " + field.Name + " = " + Format(field.GetValue(field.IsStatic ? null : instance)));
        }
        catch (Exception ex)
        {
            Line("    " + field.Name + " = <failed " + ex.GetType().Name + ">");
        }
    }
}

void Try(string label, Action action)
{
    try
    {
        action();
        Line("  " + label + ": OK");
    }
    catch (TargetInvocationException ex)
    {
        Line("  " + label + ": FAIL " + ex.InnerException?.GetType().FullName + " - " + ex.InnerException?.Message);
        PrintExceptionChain(ex, 2);
    }
    catch (Exception ex)
    {
        Line("  " + label + ": FAIL " + ex.GetType().FullName + " - " + ex.Message);
        PrintExceptionChain(ex, 2);
    }
}

void PrintExceptionChain(Exception ex, int indent)
{
    var pad = new string(' ', indent * 2);
    Line(pad + ex.GetType().FullName + ": " + ex.Message);

    if (ex is ReflectionTypeLoadException reflectionTypeLoadException)
    {
        foreach (var loaderException in reflectionTypeLoadException.LoaderExceptions)
        {
            if (loaderException != null)
            {
                PrintExceptionChain(loaderException, indent + 1);
            }
        }
    }

    if (ex.InnerException != null)
    {
        PrintExceptionChain(ex.InnerException, indent + 1);
    }
}

void DumpEnvironment(string dllPath)
{
    var dllDir = Path.GetDirectoryName(dllPath) ?? "";

    Line("ENVIRONMENT");
    Line("  AppDomain.BaseDirectory: " + AppDomain.CurrentDomain.BaseDirectory);
    Line("  CurrentDirectory:        " + Environment.CurrentDirectory);
    Line("  DLL Directory:           " + dllDir);
    Line("  ProcessPath:             " + Environment.ProcessPath);
    Line("  Debugger.IsAttached:     " + Debugger.IsAttached);
    Line("  Is64BitProcess:          " + Environment.Is64BitProcess);
    Line("  OS:                      " + Environment.OSVersion);

    foreach (var key in new[] { "NT8USERDATADIR", "NINJATRADER8_PATH", "PATH", "TEMP", "TMP" })
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            Line("  Env[" + key + "]: " + value);
        }
    }

    if (Directory.Exists(dllDir))
    {
        var agileFiles = Directory.GetFiles(dllDir, "AgileDotNetRT*.dll", SearchOption.TopDirectoryOnly);
        Line("  AgileDotNetRT in DLL dir: " + agileFiles.Length);
        foreach (var file in agileFiles)
        {
            Line("    " + file);
        }
    }

    try
    {
        using var process = Process.GetCurrentProcess();
        var modules = process.Modules
            .Cast<ProcessModule>()
            .Where(module =>
                module.ModuleName.Contains("Agile", StringComparison.OrdinalIgnoreCase) ||
                module.ModuleName.Contains("NinjaTrader", StringComparison.OrdinalIgnoreCase))
            .Select(module => module.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Line("  Loaded Agile/Ninja modules: " + modules.Count);
        foreach (var module in modules)
        {
            Line("    " + module);
        }
    }
    catch (Exception ex)
    {
        Line("  Loaded module scan failed: " + ex.GetType().Name + " - " + ex.Message);
    }
}

bool IsSimple(Type type) =>
    type.IsPrimitive ||
    type.IsEnum ||
    type == typeof(string) ||
    type == typeof(decimal);

string Format(object? value) =>
    value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        bool boolean => boolean ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };

void Line(string value = "") => report.AppendLine(value);
