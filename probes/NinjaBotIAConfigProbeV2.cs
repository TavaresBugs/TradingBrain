#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class NinjaBotIAConfigProbeV2 : Strategy
    {
        private bool ran;
        private StringBuilder report;

        private readonly string[] targetTypes =
        {
            "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAGoldBreakout_v1_0_0_0",
            "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAMomentum_v1_0_0_0",
            "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIARange_v1_0_0_0",
            "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIATrend_v1_0_0_1",
            "NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0"
        };

        protected override void OnStateChange()
        {
            if (State != State.SetDefaults)
                Print("NinjaBotIAConfigProbeV2 state: " + State);

            if (State == State.SetDefaults)
            {
                Name = "NinjaBotIAConfigProbeV2";
                Description = "Detailed config/default/attribute probe for NinjaBotIA protected strategies.";
                Calculate = Calculate.OnBarClose;
                IsInstantiatedOnEachOptimizationIteration = false;
            }
            else if (State == State.Configure && !ran)
            {
                ran = true;
                RunProbe();
            }
        }

        protected override void OnBarUpdate()
        {
        }

        private void RunProbe()
        {
            report = new StringBuilder();
            Line("=== NinjaBotIA Config Probe V2 START ===");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly ninjaBotAssembly = assemblies.FirstOrDefault(a =>
                string.Equals(a.GetName().Name, "NinjaBotIA", StringComparison.OrdinalIgnoreCase));

            if (ninjaBotAssembly == null)
            {
                Line("NinjaBotIA assembly not found in current AppDomain.");
                FinishReport();
                return;
            }

            Line("Assembly: " + ninjaBotAssembly.FullName);
            Line("Location: " + SafeLocation(ninjaBotAssembly));

            foreach (string typeName in targetTypes)
                ProbeType(ninjaBotAssembly, typeName);

            FinishReport();
        }

        private void ProbeType(Assembly assembly, string typeName)
        {
            Line("");
            Line("TYPE " + typeName);

            Type type = assembly.GetType(typeName, false);
            if (type == null)
            {
                Line("  not found");
                return;
            }

            try
            {
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                Line("  .cctor forced OK");
            }
            catch (Exception ex)
            {
                Line("  .cctor failed: " + ex.GetType().FullName + ": " + ex.Message);
            }

            object instance = null;
            try
            {
                instance = Activator.CreateInstance(type);
                Line("  instance created OK");
            }
            catch (Exception ex)
            {
                Line("  instance create failed: " + ex.GetType().FullName + ": " + ex.Message);
            }

            PrintPropertyDetails(type, instance);
            PrintSimpleFieldValues(type, instance);
        }

        private void PrintPropertyDetails(Type type, object instance)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo[] properties = type.GetProperties(flags).OrderBy(p => GetDisplayOrder(p)).ThenBy(p => p.Name).ToArray();

            Line("  PROPERTIES count=" + properties.Length);
            foreach (PropertyInfo property in properties)
            {
                DisplayAttribute display = property.GetCustomAttributes(false).OfType<DisplayAttribute>().FirstOrDefault();
                RangeAttribute range = property.GetCustomAttributes(false).OfType<RangeAttribute>().FirstOrDefault();

                Line("    PROPERTY " + property.Name);
                Line("      type: " + FriendlyType(property.PropertyType));
                Line("      visibility: " + Visibility(property));
                Line("      value: " + SafeGetValue(property, instance));
                Line("      display.name: " + SafeDisplayString(display == null ? null : display.GetName()));
                Line("      display.shortName: " + SafeDisplayString(display == null ? null : display.GetShortName()));
                Line("      display.description: " + SafeDisplayString(display == null ? null : display.GetDescription()));
                Line("      display.group: " + SafeDisplayString(display == null ? null : display.GetGroupName()));
                Line("      display.order: " + (display == null || !display.GetOrder().HasValue ? "<none>" : display.GetOrder().Value.ToString()));
                Line("      range.minimum: " + (range == null ? "<none>" : FormatObject(range.Minimum)));
                Line("      range.maximum: " + (range == null ? "<none>" : FormatObject(range.Maximum)));
                Line("      attrs: " + string.Join(", ", property.GetCustomAttributes(false).Select(a => a.GetType().FullName).ToArray()));
            }
        }

        private int GetDisplayOrder(PropertyInfo property)
        {
            DisplayAttribute display = property.GetCustomAttributes(false).OfType<DisplayAttribute>().FirstOrDefault();
            if (display != null && display.GetOrder().HasValue)
                return display.GetOrder().Value;
            return int.MaxValue;
        }

        private void PrintSimpleFieldValues(Type type, object instance)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = type.GetFields(flags).OrderBy(f => f.Name).ToArray();

            Line("  SIMPLE FIELD VALUES count=" + fields.Length);
            foreach (FieldInfo field in fields)
            {
                if (!IsSimple(field.FieldType))
                    continue;

                Line("    " + Visibility(field) + (field.IsStatic ? " static " : " ") +
                    FriendlyType(field.FieldType) + " " + field.Name +
                    " = " + SafeGetValue(field, instance));
            }
        }

        private string SafeGetValue(PropertyInfo property, object instance)
        {
            if (instance == null)
                return "<no instance>";

            MethodInfo getter = property.GetGetMethod(true);
            if (getter == null)
                return "<no getter>";

            try
            {
                object value = getter.Invoke(instance, null);
                return FormatObject(value);
            }
            catch (Exception ex)
            {
                return "<get failed: " + ex.GetType().Name + ">";
            }
        }

        private string SafeGetValue(FieldInfo field, object instance)
        {
            if (!field.IsStatic && instance == null)
                return "<no instance>";

            try
            {
                object value = field.GetValue(field.IsStatic ? null : instance);
                return FormatObject(value);
            }
            catch (Exception ex)
            {
                return "<get failed: " + ex.GetType().Name + ">";
            }
        }

        private string SafeDisplayString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "<none>";
            return "\"" + value + "\"";
        }

        private string FormatObject(object value)
        {
            if (value == null)
                return "null";
            if (value is string)
                return "\"" + value + "\"";
            if (value is bool)
                return ((bool)value) ? "true" : "false";
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private string Visibility(PropertyInfo property)
        {
            MethodInfo method = property.GetGetMethod(true) ?? property.GetSetMethod(true);
            if (method == null)
                return "unknown";
            return Visibility(method);
        }

        private string Visibility(FieldInfo field)
        {
            if (field.IsPublic)
                return "public";
            if (field.IsFamily)
                return "protected";
            if (field.IsAssembly)
                return "internal";
            return "private";
        }

        private string Visibility(MethodInfo method)
        {
            if (method.IsPublic)
                return "public";
            if (method.IsFamily)
                return "protected";
            if (method.IsAssembly)
                return "internal";
            return "private";
        }

        private bool IsSimple(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        private string FriendlyType(Type type)
        {
            if (type == typeof(int))
                return "Int32";
            if (type == typeof(double))
                return "Double";
            if (type == typeof(bool))
                return "Boolean";
            if (type == typeof(string))
                return "String";
            return type.Name;
        }

        private string SafeLocation(Assembly assembly)
        {
            try
            {
                return assembly.Location;
            }
            catch
            {
                return "<no location>";
            }
        }

        private void Line(string text)
        {
            report.AppendLine(text);
        }

        private void FinishReport()
        {
            Line("=== NinjaBotIA Config Probe V2 END ===");

            string path = "";
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string dir = Path.Combine(docs, "NinjaTrader 8", "log");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "NinjaBotIAConfigProbeV2-report.txt");
                File.WriteAllText(path, report.ToString());
            }
            catch (Exception ex)
            {
                Print("NinjaBotIAConfigProbeV2 failed to write report: " + ex.GetType().FullName + ": " + ex.Message);
            }

            Print("=== NinjaBotIA Config Probe V2 SUMMARY ===");
            Print("Report lines: " + report.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length);
            if (path.Length > 0)
                Print("Report file: " + path);
            Print("=== NinjaBotIA Config Probe V2 END ===");
        }
    }
}
