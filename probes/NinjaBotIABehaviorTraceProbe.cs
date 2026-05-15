#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class NinjaBotIABehaviorTraceProbe : Strategy
	{
		private readonly object sync = new object();
		private readonly List<Account> subscribedAccounts = new List<Account>();
		private StreamWriter writer;
		private string reportPath;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name = "NinjaBotIABehaviorTraceProbe";
				Description = "Logs account-level orders, executions and positions to reconstruct NinjaBotIA behavior.";
				Calculate = Calculate.OnEachTick;
				IsOverlay = true;
				IsUnmanaged = false;
				BarsRequiredToTrade = 1;

				AccountNameFilter = "";
				InstrumentFilter = "";
				NameContainsFilter = "";
				LogPositions = true;
				LogOnlyNinjaBotLikeNames = false;
			}
			else if (State == State.Configure)
			{
				StartTrace();
				SubscribeAllAccounts();
			}
			else if (State == State.Terminated)
			{
				UnsubscribeAllAccounts();
				StopTrace();
			}
		}

		private void StartTrace()
		{
			string logDir = Core.Globals.UserDataDir;
			reportPath = Path.Combine(logDir, "log", "NinjaBotIABehaviorTraceProbe-events.csv");

			lock (sync)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
				writer = new StreamWriter(reportPath, true, Encoding.UTF8);
				if (new FileInfo(reportPath).Length == 0)
					writer.WriteLine("utc,local,event,operation,account,instrument,orderName,entrySignal,oco,action,type,state,quantity,filled,avgFill,price,limit,stop,marketPosition,orderId,executionId,comment");

				WriteRaw("TRACE_START", "", "", "", "", "", "", "", "", "", 0, 0, 0, 0, 0, 0, "", "", "", "Probe configured");
				writer.Flush();
			}

			Print("NinjaBotIABehaviorTraceProbe logging to: " + reportPath);
		}

		private void StopTrace()
		{
			lock (sync)
			{
				if (writer == null)
					return;

				WriteRaw("TRACE_STOP", "", "", "", "", "", "", "", "", "", 0, 0, 0, 0, 0, 0, "", "", "", "Probe terminated");
				writer.Flush();
				writer.Dispose();
				writer = null;
			}
		}

		private void SubscribeAllAccounts()
		{
			lock (Account.All)
			{
				foreach (Account account in Account.All)
				{
					if (!ShouldUseAccount(account))
						continue;

					account.OrderUpdate += OnAccountOrderUpdate;
					account.ExecutionUpdate += OnAccountExecutionUpdate;
					if (LogPositions)
						account.PositionUpdate += OnAccountPositionUpdate;

					subscribedAccounts.Add(account);
					WriteRaw("SUBSCRIBE", "", account.DisplayName, "", "", "", "", "", "", "", 0, 0, 0, 0, 0, 0, "", "", "", "Subscribed account");
				}
			}
		}

		private void UnsubscribeAllAccounts()
		{
			foreach (Account account in subscribedAccounts.ToList())
			{
				account.OrderUpdate -= OnAccountOrderUpdate;
				account.ExecutionUpdate -= OnAccountExecutionUpdate;
				account.PositionUpdate -= OnAccountPositionUpdate;
			}

			subscribedAccounts.Clear();
		}

		private void OnAccountOrderUpdate(object sender, OrderEventArgs e)
		{
			Order order = e.Order;
			if (!ShouldLogOrder(order))
				return;

			WriteRaw(
				"ORDER",
				"",
				SafeAccount(order == null ? null : order.Account),
				SafeInstrument(order == null ? null : order.Instrument),
				Safe(order == null ? null : order.Name),
				Safe(order == null ? null : order.FromEntrySignal),
				Safe(order == null ? null : order.Oco),
				order == null ? "" : Safe(order.OrderAction),
				order == null ? "" : Safe(order.OrderType),
				Safe(e.OrderState),
				e.Quantity,
				e.Filled,
				e.AverageFillPrice,
				0,
				e.LimitPrice,
				e.StopPrice,
				"",
				Safe(e.OrderId),
				"",
				Safe(e.Comment));
		}

		private void OnAccountExecutionUpdate(object sender, ExecutionEventArgs e)
		{
			Execution execution = e.Execution;
			Order order = execution == null ? null : execution.Order;
			if (!ShouldLogExecution(execution))
				return;

			WriteRaw(
				"EXECUTION",
				Safe(e.Operation),
				SafeAccount(execution == null ? null : execution.Account),
				SafeInstrument(execution == null ? null : execution.Instrument),
				Safe(order == null ? (execution == null ? null : execution.Name) : order.Name),
				Safe(order == null ? null : order.FromEntrySignal),
				Safe(order == null ? null : order.Oco),
				order == null ? "" : Safe(order.OrderAction),
				order == null ? "" : Safe(order.OrderType),
				order == null ? "" : Safe(order.OrderState),
				e.Quantity,
				order == null ? 0 : order.Filled,
				order == null ? 0 : order.AverageFillPrice,
				e.Price,
				order == null ? 0 : order.LimitPrice,
				order == null ? 0 : order.StopPrice,
				Safe(e.MarketPosition),
				Safe(e.OrderId),
				Safe(e.ExecutionId),
				"");
		}

		private void OnAccountPositionUpdate(object sender, PositionEventArgs e)
		{
			Position position = e.Position;
			if (position == null || !ShouldUseAccount(position.Account) || !ShouldUseInstrument(position.Instrument))
				return;

			WriteRaw(
				"POSITION",
				Safe(e.Operation),
				SafeAccount(position.Account),
				SafeInstrument(position.Instrument),
				"",
				"",
				"",
				"",
				"",
				"",
				e.Quantity,
				0,
				e.AveragePrice,
				0,
				0,
				0,
				Safe(e.MarketPosition),
				"",
				"",
				"");
		}

		private bool ShouldLogOrder(Order order)
		{
			if (order == null || !ShouldUseAccount(order.Account) || !ShouldUseInstrument(order.Instrument))
				return false;

			return ShouldUseName(order.Name, order.FromEntrySignal);
		}

		private bool ShouldLogExecution(Execution execution)
		{
			if (execution == null || !ShouldUseAccount(execution.Account) || !ShouldUseInstrument(execution.Instrument))
				return false;

			Order order = execution.Order;
			return ShouldUseName(order == null ? execution.Name : order.Name, order == null ? "" : order.FromEntrySignal);
		}

		private bool ShouldUseAccount(Account account)
		{
			return account != null
				&& (string.IsNullOrWhiteSpace(AccountNameFilter) || string.Equals(account.DisplayName, AccountNameFilter, StringComparison.OrdinalIgnoreCase) || string.Equals(account.Name, AccountNameFilter, StringComparison.OrdinalIgnoreCase));
		}

		private bool ShouldUseInstrument(Instrument instrument)
		{
			if (instrument == null || string.IsNullOrWhiteSpace(InstrumentFilter))
				return true;

			return Contains(instrument.FullName, InstrumentFilter) || Contains(instrument.MasterInstrument == null ? "" : instrument.MasterInstrument.Name, InstrumentFilter);
		}

		private bool ShouldUseName(string orderName, string entrySignal)
		{
			string haystack = (orderName ?? "") + " " + (entrySignal ?? "");

			if (LogOnlyNinjaBotLikeNames && !Contains(haystack, "NinjaBotIA"))
				return false;

			return string.IsNullOrWhiteSpace(NameContainsFilter) || Contains(haystack, NameContainsFilter);
		}

		private static bool Contains(string text, string value)
		{
			return (text ?? "").IndexOf(value ?? "", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private void WriteRaw(string eventName, string operation, string account, string instrument, string orderName, string entrySignal, string oco, string action, string type, string state, int quantity, int filled, double avgFill, double price, double limit, double stop, string marketPosition, string orderId, string executionId, string comment)
		{
			lock (sync)
			{
				if (writer == null)
					return;

				writer.WriteLine(string.Join(",",
					Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
					Csv(DateTime.Now.ToString("o", CultureInfo.InvariantCulture)),
					Csv(eventName),
					Csv(operation),
					Csv(account),
					Csv(instrument),
					Csv(orderName),
					Csv(entrySignal),
					Csv(oco),
					Csv(action),
					Csv(type),
					Csv(state),
					quantity.ToString(CultureInfo.InvariantCulture),
					filled.ToString(CultureInfo.InvariantCulture),
					avgFill.ToString(CultureInfo.InvariantCulture),
					price.ToString(CultureInfo.InvariantCulture),
					limit.ToString(CultureInfo.InvariantCulture),
					stop.ToString(CultureInfo.InvariantCulture),
					Csv(marketPosition),
					Csv(orderId),
					Csv(executionId),
					Csv(comment)));

				writer.Flush();
			}
		}

		private static string Safe(object value)
		{
			return value == null ? "" : value.ToString();
		}

		private static string SafeAccount(Account account)
		{
			return account == null ? "" : account.DisplayName;
		}

		private static string SafeInstrument(Instrument instrument)
		{
			return instrument == null ? "" : instrument.FullName;
		}

		private static string Csv(string value)
		{
			value = value ?? "";
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}

		[NinjaScriptProperty]
		[Display(Name = "Account filter", GroupName = "Trace", Order = 1)]
		public string AccountNameFilter
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Instrument filter", GroupName = "Trace", Order = 2)]
		public string InstrumentFilter
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Name contains filter", GroupName = "Trace", Order = 3)]
		public string NameContainsFilter
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Log positions", GroupName = "Trace", Order = 4)]
		public bool LogPositions
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Only NinjaBotIA names", GroupName = "Trace", Order = 5)]
		public bool LogOnlyNinjaBotLikeNames
		{ get; set; }
	}
}
