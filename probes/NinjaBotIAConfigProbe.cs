#region Using declarations
using System;
using System.ComponentModel;
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
    public class NinjaBotIAConfigProbe : Strategy
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
                Print("NinjaBotIAConfigProbe state: " + State);

            if (State == State.SetDefaults)
            {
                Name = "NinjaBotIAConfigProbe";
                Description = "Configuration/default probe for NinjaBotIA protected strategies.";
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
            Line("=== NinjaBotIA Config Probe START ===");

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

            PrintProperties(type, instance);
            PrintFields(type, instance);
        }

        private void PrintProperties(Type type, object instance)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo[] properties = type.GetProperties(flags).OrderBy(p => p.Name).ToArray();

            Line("  PROPERTIES count=" + properties.Length);
            foreach (PropertyInfo property in properties)
            {
                Line("    " + Visibility(property) + " " + FriendlyType(property.PropertyType) + " " + property.Name +
                    " value=" + SafeGetValue(property, instance));

                object[] attrs = property.GetCustomAttributes(false);
                if (attrs.Length == 0)
                {
                    Line("      attrs: none");
                }
                else
                {
                    foreach (object attr in attrs)
                        Line("      attr: " + FormatAttribute(attr));
                }
            }
        }

        private void PrintFields(Type type, object instance)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = type.GetFields(flags).OrderBy(f => f.Name).ToArray();

            Line("  FIELDS count=" + fields.Length);
            foreach (FieldInfo field in fields)
            {
                Line("    " + Visibility(field) + (field.IsStatic ? " static " : " ") +
                    FriendlyType(field.FieldType) + " " + field.Name +
                    " value=" + SafeGetValue(field, instance));
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
                return FormatValue(value);
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

            if (!IsSimple(field.FieldType))
                return "<" + FriendlyType(field.FieldType) + ">";

            try
            {
                object value = field.GetValue(field.IsStatic ? null : instance);
                return FormatValue(value);
            }
            catch (Exception ex)
            {
                return "<get failed: " + ex.GetType().Name + ">";
            }
        }

        private string FormatAttribute(object attr)
        {
            if (attr == null)
                return "<null>";

            Type type = attr.GetType();
            string name = type.FullName;

            try
            {
                DisplayNameAttribute displayName = attr as DisplayNameAttribute;
                if (displayName != null)
                    return name + "(DisplayName=" + displayName.DisplayName + ")";

                CategoryAttribute category = attr as CategoryAttribute;
                if (category != null)
                    return name + "(Category=" + category.Category + ")";

                DescriptionAttribute description = attr as DescriptionAttribute;
                if (description != null)
                    return name + "(Description=" + description.Description + ")";

                BrowsableAttribute browsable = attr as BrowsableAttribute;
                if (browsable != null)
                    return name + "(Browsable=" + browsable.Browsable + ")";

                return name;
            }
            catch
            {
                return name;
            }
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

        private string FormatValue(object value)
        {
            if (value == null)
                return "null";
            if (value is string)
                return "\"" + value + "\"";
            if (value is bool)
                return ((bool)value) ? "true" : "false";
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
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
            Line("=== NinjaBotIA Config Probe END ===");

            string path = "";
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string dir = Path.Combine(docs, "NinjaTrader 8", "log");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "NinjaBotIAConfigProbe-report.txt");
                File.WriteAllText(path, report.ToString());
            }
            catch (Exception ex)
            {
                Print("NinjaBotIAConfigProbe failed to write report: " + ex.GetType().FullName + ": " + ex.Message);
            }

            Print("=== NinjaBotIA Config Probe SUMMARY ===");
            Print("Report lines: " + report.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length);
            if (path.Length > 0)
                Print("Report file: " + path);
            Print("=== NinjaBotIA Config Probe END ===");
        }
    }
}
