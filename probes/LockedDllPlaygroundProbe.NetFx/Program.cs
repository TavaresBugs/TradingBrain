using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LockedDllPlaygroundProbeNetFx
{
    internal static class Program
    {
        private static readonly string NinjaBin = @"C:\Program Files\NinjaTrader 8\bin";
        private static readonly string CustomBin = @"C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\bin\Custom";
        private static readonly string TargetPath = Path.Combine(CustomBin, "NinjaBotIA.dll");
        private static readonly string ReportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locked-dll-playground-netfx-report.txt");
        private static readonly StringBuilder Report = new StringBuilder();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static void Main()
        {
            Console.Title = "Locked DLL Playground Probe (.NET Framework)";
            Console.OutputEncoding = Encoding.UTF8;

            PrepareEnvironment();

            Line("=== LOCKED DLL PLAYGROUND PROBE NETFX ===");
            Line("Target: " + TargetPath);
            Line("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Line();
            DumpEnvironment();
            Line();

            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            try
            {
                PreloadNinjaAssemblies();

                Assembly assembly = Assembly.LoadFrom(TargetPath);
                Line("Assembly loaded: " + assembly.FullName);
                Try("assembly.GetTypes", delegate
                {
                    Type[] types = assembly.GetTypes();
                    Line("  assembly.GetTypes count: " + types.Length);
                });
                Line();

                string[] targetTypes =
                {
                    "NinjaTrader.NinjaScript.Strategies.ema",
                    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAOrbBreakout_v1_0_0_0",
                    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAMomentum_v1_0_0_0",
                    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIARange_v1_0_0_0",
                    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIATrend_v1_0_0_1",
                    "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0"
                };

                foreach (string typeName in targetTypes)
                    ProbeType(assembly, typeName);
            }
            catch (Exception ex)
            {
                Line("FATAL");
                PrintExceptionChain(ex, 1);
            }

            File.WriteAllText(ReportPath, Report.ToString(), Encoding.UTF8);
            Console.WriteLine(Report.ToString());
            Console.WriteLine("Report: " + ReportPath);
        }

        private static void PrepareEnvironment()
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", NinjaBin + ";" + CustomBin + ";" + path);
            Environment.SetEnvironmentVariable("NT8USERDATADIR", @"C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\");
            Environment.SetEnvironmentVariable("NINJATRADER8_PATH", @"C:\Program Files\NinjaTrader 8");
            Environment.SetEnvironmentVariable("COMPLUS_Version", "v4.0.30319");
            SetDllDirectory(NinjaBin);
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            string fileName = new AssemblyName(args.Name).Name + ".dll";
            foreach (string dir in new[] { CustomBin, NinjaBin })
            {
                string path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }

            return null;
        }

        private static void PreloadNinjaAssemblies()
        {
            string[] assemblyNames =
            {
                "NinjaTrader.Core.dll",
                "NinjaTrader.Gui.dll",
                "NinjaTrader.Custom.dll",
                "NinjaTrader.Vendor.dll"
            };

            Line("PRELOAD");
            foreach (string assemblyName in assemblyNames)
            {
                string dir = assemblyName == "NinjaTrader.Custom.dll" || assemblyName == "NinjaTrader.Vendor.dll"
                    ? CustomBin
                    : NinjaBin;
                string path = Path.Combine(dir, assemblyName);
                if (!File.Exists(path))
                {
                    Line("  missing " + path);
                    continue;
                }

                Try("preload " + assemblyName, delegate
                {
                    Assembly loaded = Assembly.LoadFrom(path);
                    Line("    " + loaded.FullName);
                });
            }
            Line();
        }

        private static void ProbeType(Assembly assembly, string typeName)
        {
            Line("TYPE " + typeName);
            Type type = assembly.GetType(typeName, false);
            if (type == null)
            {
                Line("  not found");
                Line();
                return;
            }

            Try("force .cctor", delegate { RuntimeHelpers.RunClassConstructor(type.TypeHandle); });

            object instance = null;
            Try("create instance", delegate { instance = Activator.CreateInstance(type); });

            if (instance == null)
            {
                Line("  instance unavailable; skipping lifecycle calls");
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

        private static void TryCallLifecycle(Type type, object instance, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                Line("  " + methodName + ": not found");
                return;
            }

            byte[] il = method.GetMethodBody() == null ? null : method.GetMethodBody().GetILAsByteArray();
            Line("  " + methodName + " IL length: " + (il == null ? "<none>" : il.Length.ToString()));
            Try("call " + methodName, delegate { method.Invoke(instance, null); });
        }

        private static void PrintSimpleProperties(Type type, object instance)
        {
            Line("  PROPERTIES");
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(p => IsSimple(p.PropertyType)).OrderBy(p => p.Name))
            {
                MethodInfo getter = property.GetGetMethod(true);
                if (getter == null)
                {
                    Line("    " + property.Name + " = <no getter>");
                    continue;
                }

                Try("get property " + property.Name, delegate { Line("    " + property.Name + " = " + Format(getter.Invoke(instance, null))); });
            }
        }

        private static void PrintSimpleFields(Type type, object instance, string label)
        {
            Line("  SIMPLE FIELDS " + label);
            foreach (FieldInfo field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(f => IsSimple(f.FieldType)).OrderBy(f => f.Name))
            {
                Try("get field " + field.Name, delegate { Line("    " + field.Name + " = " + Format(field.GetValue(field.IsStatic ? null : instance))); });
            }
        }

        private static void Try(string label, Action action)
        {
            try
            {
                action();
                Line("  " + label + ": OK");
            }
            catch (TargetInvocationException ex)
            {
                Line("  " + label + ": FAIL " + (ex.InnerException == null ? ex.GetType().FullName : ex.InnerException.GetType().FullName) + " - " + (ex.InnerException == null ? ex.Message : ex.InnerException.Message));
                PrintExceptionChain(ex, 2);
            }
            catch (Exception ex)
            {
                Line("  " + label + ": FAIL " + ex.GetType().FullName + " - " + ex.Message);
                PrintExceptionChain(ex, 2);
            }
        }

        private static void PrintExceptionChain(Exception ex, int indent)
        {
            string pad = new string(' ', indent * 2);
            Line(pad + ex.GetType().FullName + ": " + ex.Message);
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                Line(pad + "STACK: " + ex.StackTrace.Replace(Environment.NewLine, Environment.NewLine + pad + "       "));

            ReflectionTypeLoadException rtle = ex as ReflectionTypeLoadException;
            if (rtle != null)
            {
                foreach (Exception loaderException in rtle.LoaderExceptions)
                    if (loaderException != null)
                        PrintExceptionChain(loaderException, indent + 1);
            }

            if (ex.InnerException != null)
                PrintExceptionChain(ex.InnerException, indent + 1);
        }

        private static void DumpEnvironment()
        {
            Line("ENVIRONMENT");
            Line("  AppDomain.BaseDirectory: " + AppDomain.CurrentDomain.BaseDirectory);
            Line("  CurrentDirectory:        " + Environment.CurrentDirectory);
            Line("  Target DLL Directory:    " + CustomBin);
            Line("  Runtime Version:         " + Environment.Version);
            Line("  Debugger.IsAttached:     " + Debugger.IsAttached);
            Line("  Is64BitProcess:          " + Environment.Is64BitProcess);
            Line("  OS:                      " + Environment.OSVersion);

            string[] agileFiles = Directory.Exists(NinjaBin) ? Directory.GetFiles(NinjaBin, "AgileDotNetRT*.dll", SearchOption.TopDirectoryOnly) : new string[0];
            Line("  AgileDotNetRT in Ninja bin: " + agileFiles.Length);
            foreach (string file in agileFiles)
                Line("    " + file);

            string[] customAgileFiles = Directory.Exists(CustomBin) ? Directory.GetFiles(CustomBin, "AgileDotNetRT*.dll", SearchOption.TopDirectoryOnly) : new string[0];
            Line("  AgileDotNetRT in Custom bin: " + customAgileFiles.Length);
            foreach (string file in customAgileFiles)
                Line("    " + file);

            string[] keys = { "NT8USERDATADIR", "NINJATRADER8_PATH", "PATH", "TEMP", "TMP" };
            foreach (string key in keys)
            {
                string value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(value))
                    Line("  Env[" + key + "]: " + value);
            }

            try
            {
                var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                    .Where(m => m.ModuleName.IndexOf("Agile", StringComparison.OrdinalIgnoreCase) >= 0 || m.ModuleName.IndexOf("NinjaTrader", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(m => m.FileName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Line("  Loaded Agile/Ninja modules: " + modules.Count);
                foreach (string module in modules)
                    Line("    " + module);
            }
            catch (Exception ex)
            {
                Line("  Loaded module scan failed: " + ex.GetType().Name + " - " + ex.Message);
            }
        }

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        private static string Format(object value)
        {
            if (value == null)
                return "null";
            if (value is string)
                return "\"" + value + "\"";
            if (value is bool)
                return ((bool)value) ? "true" : "false";
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void Line()
        {
            Report.AppendLine();
        }

        private static void Line(string value)
        {
            Report.AppendLine(value);
        }
    }
}
