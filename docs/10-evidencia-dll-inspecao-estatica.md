# DLL Metadata Inspection

- Assembly: `NinjaBotIA`
- Path: `C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\bin\Custom\NinjaBotIA.dll`
- Generated: `2026-05-14 18:56:56`
- Mode: static metadata read only; no execution, no patching, no instantiation.

## Assembly References

- `mscorlib` `4.0.0.0`
- `netstandard` `2.0.0.0`
- `NinjaTrader.Core` `8.1.5.2`
- `NinjaTrader.Gui` `8.1.5.2`
- `NinjaTrader.Vendor` `8.1.5.2`
- `PresentationCore` `4.0.0.0`
- `PresentationFramework` `4.0.0.0`
- `SharpDX` `2.6.3.0`
- `SharpDX.Direct2D1` `2.6.3.0`
- `System` `4.0.0.0`
- `System.ComponentModel.DataAnnotations` `4.0.0.0`
- `System.Core` `4.0.0.0`
- `System.Xml` `4.0.0.0`
- `WindowsBase` `4.0.0.0`

## Module Initializer

- `<Module>.cctor`: found
- Has body: `True`
- IL instructions: `5`

## Strategy Types (6)

### `NinjaTrader.NinjaScript.Strategies.ema`

- Base: `NinjaTrader.NinjaScript.Strategies.Strategy`
- Token: `0x0200004C`

#### Fields
- `0x04000156` `NinjaTrader.NinjaScript.Indicators.Swing` `Swing1`
- `0x04000157` `System.Int32` `<Stop>k__BackingField`
- `0x04000158` `System.Int32` `<Target>k__BackingField`

#### Properties
- `System.Int32` `Stop`
- `System.Int32` `Target`

#### `OnStateChange`
- Token: `0x06000960`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### `OnBarUpdate`
- Token: `0x06000961`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### Recognized Trading Calls
- none found

### `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAOrbBreakout_v1_0_0_0`

- Base: `NinjaTrader.NinjaScript.Strategies.Strategy`
- Token: `0x0200004D`

#### Fields
- `0x04000159` `System.Int32` `<LoteBase>k__BackingField`
- `0x0400015A` `System.Int32` `<StartHour>k__BackingField`
- `0x0400015B` `System.Int32` `<StartMinute>k__BackingField`
- `0x0400015C` `System.Int32` `<EndHour>k__BackingField`
- `0x0400015D` `System.Int32` `<EndMinute>k__BackingField`
- `0x0400015E` `System.Int32` `<CloseAllHour>k__BackingField`
- `0x0400015F` `System.Int32` `<CloseAllMinute>k__BackingField`
- `0x04000160` `System.Boolean` `<UsePartial>k__BackingField`
- `0x04000161` `System.Int32` `<Take1Ticks>k__BackingField`
- `0x04000162` `System.Int32` `<Take2Ticks>k__BackingField`
- `0x04000163` `System.Int32` `<StopLossTicks>k__BackingField`
- `0x04000164` `System.Double` `windowHigh`
- `0x04000165` `System.Boolean` `windowActive`
- `0x04000166` `System.Int32` `tradesHoje`
- `0x04000167` `System.Boolean` `podeVender`
- `0x04000168` `System.Double` `ultimoLucro`
- `0x04000169` `System.Int32` `loteAtual`
- `0x0400016B` `System.Int32` `StartHHmmss`
- `0x0400016C` `System.Int32` `EndHHmmss`
- `0x0400016D` `System.Int32` `CloseAllHHmmss`

#### Properties
- `System.Int32` `CloseAllHour`
- `System.Int32` `CloseAllMinute`
- `System.Int32` `EndHour`
- `System.Int32` `EndMinute`
- `System.Int32` `LoteBase`
- `System.Int32` `StartHour`
- `System.Int32` `StartMinute`
- `System.Int32` `StopLossTicks`
- `System.Int32` `Take1Ticks`
- `System.Int32` `Take2Ticks`
- `System.Boolean` `UsePartial`

#### `OnStateChange`
- Token: `0x0600097F`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### `OnBarUpdate`
- Token: `0x06000980`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### Recognized Trading Calls
- none found

### `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAMomentum_v1_0_0_0`

- Base: `NinjaTrader.NinjaScript.Strategies.Strategy`
- Token: `0x0200004E`

#### Fields
- `0x0400016E` `System.Int32` `<Quantity>k__BackingField`
- `0x0400016F` `System.Int32` `<StartHour>k__BackingField`
- `0x04000170` `System.Int32` `<StartMinute>k__BackingField`
- `0x04000171` `System.Int32` `<EndHour>k__BackingField`
- `0x04000172` `System.Int32` `<EndMinute>k__BackingField`
- `0x04000173` `System.Int32` `<Lb>k__BackingField`
- `0x04000174` `System.Int32` `<MacdFast>k__BackingField`
- `0x04000175` `System.Int32` `<MacdSlow>k__BackingField`
- `0x04000176` `System.Int32` `<MacdSignal>k__BackingField`
- `0x04000177` `System.Int32` `<StopLossTicks>k__BackingField`
- `0x04000178` `System.Int32` `<ProfitTargetTicks>k__BackingField`
- `0x04000179` `NinjaTrader.NinjaScript.Series`1<System.Double>` `upSeries`
- `0x0400017A` `NinjaTrader.NinjaScript.Series`1<System.Double>` `dnSeries`
- `0x0400017B` `NinjaTrader.NinjaScript.Series`1<System.Double>` `baseSeries`
- `0x0400017C` `NinjaTrader.NinjaScript.Series`1<System.Int32>` `stateCh`
- `0x0400017D` `NinjaTrader.NinjaScript.Series`1<System.Double>` `mg_H`
- `0x0400017E` `NinjaTrader.NinjaScript.Series`1<System.Double>` `mg_L`
- `0x0400017F` `NinjaTrader.NinjaScript.Series`1<System.Double>` `mg_C`
- `0x04000180` `NinjaTrader.NinjaScript.Series`1<System.Double>` `ss_H`
- `0x04000181` `NinjaTrader.NinjaScript.Series`1<System.Double>` `ss_L`
- `0x04000182` `NinjaTrader.NinjaScript.Series`1<System.Double>` `ss_C`
- `0x04000183` `NinjaTrader.NinjaScript.Series`1<System.Double>` `smma_H`
- `0x04000184` `NinjaTrader.NinjaScript.Series`1<System.Double>` `smma_L`
- `0x04000185` `NinjaTrader.NinjaScript.Series`1<System.Double>` `smma_C`
- `0x04000186` `NinjaTrader.NinjaScript.Series`1<System.Double>` `tmp_H`
- `0x04000187` `NinjaTrader.NinjaScript.Series`1<System.Double>` `tmp_L`
- `0x04000188` `NinjaTrader.NinjaScript.Series`1<System.Double>` `tmp_C`
- `0x04000189` `System.Windows.Media.Brush` `bullBrush`
- `0x0400018A` `System.Windows.Media.Brush` `bearBrush`
- `0x0400018B` `NinjaTrader.NinjaScript.Indicators.MACD` `macdI`
- `0x0400018C` `System.String` `MAKind`
- `0x0400018D` `System.Boolean` `ShowCloud`
- `0x0400018E` `System.Boolean` `ShowArrows`
- `0x0400018F` `System.Boolean` `UseTimeFilter`
- `0x04000190` `System.Boolean` `FlatWhenOutside`

#### Properties
- `System.Int32` `EndHour`
- `System.Int32` `EndMinute`
- `System.Int32` `Lb`
- `System.Int32` `MacdFast`
- `System.Int32` `MacdSignal`
- `System.Int32` `MacdSlow`
- `System.Int32` `ProfitTargetTicks`
- `System.Int32` `Quantity`
- `System.Int32` `StartHour`
- `System.Int32` `StartMinute`
- `System.Int32` `StopLossTicks`

#### `OnStateChange`
- Token: `0x0600099B`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### `OnBarUpdate`
- Token: `0x0600099C`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### Recognized Trading Calls
- none found

### `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIARange_v1_0_0_0`

- Base: `NinjaTrader.NinjaScript.Strategies.Strategy`
- Token: `0x0200004F`

#### Fields
- `0x04000191` `NinjaTrader.NinjaScript.Series`1<System.Double>` `avrng`
- `0x04000192` `NinjaTrader.NinjaScript.Series`1<System.Double>` `acSeries`
- `0x04000193` `NinjaTrader.NinjaScript.Series`1<System.Double>` `rfilt`
- `0x04000194` `NinjaTrader.NinjaScript.Series`1<System.Double>` `filtDir`
- `0x04000195` `NinjaTrader.NinjaScript.Series`1<System.Int32>` `condIni`
- `0x04000196` `System.Int32` `<contractSize>k__BackingField`
- `0x04000197` `System.Int32` `<StartHour>k__BackingField`
- `0x04000198` `System.Int32` `<StartMinute>k__BackingField`
- `0x04000199` `System.Int32` `<EndHour>k__BackingField`
- `0x0400019A` `System.Int32` `<EndMinute>k__BackingField`
- `0x0400019B` `System.Int32` `<SwingPeriod>k__BackingField`
- `0x0400019C` `System.Double` `<SwingMultiplier>k__BackingField`
- `0x0400019D` `System.Double` `<FilteATR>k__BackingField`
- `0x0400019E` `System.Int32` `<ATRPeriod>k__BackingField`
- `0x0400019F` `System.Double` `<ATRMultiplierTP>k__BackingField`
- `0x040001A0` `System.Double` `<ATRMultiplierSL>k__BackingField`
- `0x040001A1` `NinjaTrader.NinjaScript.Indicators.ATR` `atr`

#### Properties
- `System.Double` `ATRMultiplierSL`
- `System.Double` `ATRMultiplierTP`
- `System.Int32` `ATRPeriod`
- `System.Int32` `contractSize`
- `System.Int32` `EndHour`
- `System.Int32` `EndMinute`
- `System.Double` `FilteATR`
- `System.Int32` `StartHour`
- `System.Int32` `StartMinute`
- `System.Double` `SwingMultiplier`
- `System.Int32` `SwingPeriod`

#### `OnStateChange`
- Token: `0x060009BD`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### `OnBarUpdate`
- Token: `0x060009BE`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### Recognized Trading Calls
- none found

### `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIATrend_v1_0_0_1`

- Base: `NinjaTrader.NinjaScript.Strategies.Strategy`
- Token: `0x02000050`

#### Fields
- `0x040001A2` `System.Int32` `<contractSize>k__BackingField`
- `0x040001A3` `System.Int32` `<StartHour>k__BackingField`
- `0x040001A4` `System.Int32` `<StartMinute>k__BackingField`
- `0x040001A5` `System.Int32` `<EndHour>k__BackingField`
- `0x040001A6` `System.Int32` `<EndMinute>k__BackingField`
- `0x040001A7` `System.Int32` `<Periods>k__BackingField`
- `0x040001A8` `System.Double` `<Multiplier>k__BackingField`
- `0x040001A9` `System.Boolean` `<ChangeATR>k__BackingField`
- `0x040001AA` `System.Int32` `<TargetTicks>k__BackingField`
- `0x040001AB` `System.Int32` `<StopTicks>k__BackingField`
- `0x040001AC` `System.Boolean` `<UseBreakEven>k__BackingField`
- `0x040001AD` `System.Int32` `<BreakEvenTriggerTicks>k__BackingField`
- `0x040001AE` `System.Int32` `<BreakEvenPlusTicks>k__BackingField`
- `0x040001AF` `System.Boolean` `<UseTrailing>k__BackingField`
- `0x040001B0` `System.Int32` `<TrailTriggerTicks>k__BackingField`
- `0x040001B1` `System.Int32` `<TrailDistanceTicks>k__BackingField`
- `0x040001B2` `NinjaTrader.NinjaScript.Series`1<System.Double>` `upSeries`
- `0x040001B3` `NinjaTrader.NinjaScript.Series`1<System.Double>` `dnSeries`
- `0x040001B4` `NinjaTrader.NinjaScript.Series`1<System.Int32>` `trendSeries`
- `0x040001B5` `NinjaTrader.NinjaScript.Series`1<System.Double>` `trSeries`
- `0x040001B6` `NinjaTrader.NinjaScript.Indicators.SMA` `smaTR`
- `0x040001B7` `System.Boolean` `beLongApplied`
- `0x040001B8` `System.Boolean` `beShortApplied`
- `0x040001B9` `System.Double` `curStopLong`
- `0x040001BA` `System.Double` `curStopShort`

#### Properties
- `System.Int32` `BreakEvenPlusTicks`
- `System.Int32` `BreakEvenTriggerTicks`
- `System.Boolean` `ChangeATR`
- `System.Int32` `contractSize`
- `System.Int32` `EndHour`
- `System.Int32` `EndMinute`
- `System.Double` `Multiplier`
- `System.Int32` `Periods`
- `System.Int32` `StartHour`
- `System.Int32` `StartMinute`
- `System.Int32` `StopTicks`
- `System.Int32` `TargetTicks`
- `System.Int32` `TrailDistanceTicks`
- `System.Int32` `TrailTriggerTicks`
- `System.Boolean` `UseBreakEven`
- `System.Boolean` `UseTrailing`

#### `OnStateChange`
- Token: `0x060009E2`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### `OnBarUpdate`
- Token: `0x060009E3`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### Recognized Trading Calls
- none found

### `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`

- Base: `NinjaTrader.NinjaScript.Strategies.Strategy`
- Token: `0x02000051`

#### Fields
- `0x040001BB` `System.Int32` `<contractSize>k__BackingField`
- `0x040001BC` `System.Int32` `<SessionStartHour>k__BackingField`
- `0x040001BD` `System.Int32` `<SessionStartMinute>k__BackingField`
- `0x040001BE` `System.Int32` `<SessionStartSecond>k__BackingField`
- `0x040001BF` `System.Int32` `<SessionEndHour>k__BackingField`
- `0x040001C0` `System.Int32` `<SessionEndMinute>k__BackingField`
- `0x040001C1` `System.Int32` `<SessionEndSecond>k__BackingField`
- `0x040001C2` `System.Int32` `<EMA1Period>k__BackingField`
- `0x040001C3` `System.Int32` `<EMA2Period>k__BackingField`
- `0x040001C4` `System.Int32` `<RSIPeriod>k__BackingField`
- `0x040001C5` `System.Int32` `<RSISmooth>k__BackingField`
- `0x040001C6` `System.Int32` `<ATRPeriod>k__BackingField`
- `0x040001C7` `System.Int32` `<ATRSMAPeriod>k__BackingField`
- `0x040001C8` `System.Int32` `<SMAVolumePeriod>k__BackingField`
- `0x040001C9` `System.Double` `<TrailStopMultiplier>k__BackingField`
- `0x040001CA` `System.Double` `<BreakEvenMultiplier>k__BackingField`
- `0x040001CB` `System.Int32` `<TimeExitBars>k__BackingField`
- `0x040001CC` `System.Int32` `<MaxDrawdown>k__BackingField`
- `0x040001CD` `NinjaTrader.NinjaScript.Indicators.EMA` `EMA1`
- `0x040001CE` `NinjaTrader.NinjaScript.Indicators.EMA` `EMA2`
- `0x040001CF` `NinjaTrader.NinjaScript.Indicators.RSI` `RSI1`
- `0x040001D0` `NinjaTrader.NinjaScript.Indicators.VWAPNinjaBotIA` `VWAP81`
- `0x040001D1` `NinjaTrader.NinjaScript.Indicators.SMA` `SMA1`
- `0x040001D2` `NinjaTrader.NinjaScript.Indicators.SMA` `SMA2`
- `0x040001D3` `NinjaTrader.NinjaScript.Indicators.ATR` `STR1`
- `0x040001D4` `System.Double` `volatilityFilter`
- `0x040001D5` `System.Double` `ATR1`
- `0x040001D6` `System.Double` `trailStop`
- `0x040001D7` `System.Double` `breakEvenTrigger`
- `0x040001D8` `System.Int32` `longEntryBar`
- `0x040001D9` `System.Int32` `shortEntryBar`
- `0x040001DA` `System.Double` `longEntryPrice`
- `0x040001DB` `System.Double` `shortEntryPrice`
- `0x040001DC` `System.Double` `maxDrawdownPercent`
- `0x040001DD` `System.Double` `highestProfit`

#### Properties
- `System.Int32` `ATRPeriod`
- `System.Int32` `ATRSMAPeriod`
- `System.Double` `BreakEvenMultiplier`
- `System.Int32` `contractSize`
- `System.Int32` `EMA1Period`
- `System.Int32` `EMA2Period`
- `System.Int32` `MaxDrawdown`
- `System.Int32` `RSIPeriod`
- `System.Int32` `RSISmooth`
- `System.Int32` `SessionEndHour`
- `System.Int32` `SessionEndMinute`
- `System.Int32` `SessionEndSecond`
- `System.Int32` `SessionStartHour`
- `System.Int32` `SessionStartMinute`
- `System.Int32` `SessionStartSecond`
- `System.Int32` `SMAVolumePeriod`
- `System.Int32` `TimeExitBars`
- `System.Double` `TrailStopMultiplier`

#### `OnStateChange`
- Token: `0x06000A0B`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### `OnBarUpdate`
- Token: `0x06000A0C`
- Has body: `True`
- IL instructions: `1`
- Ret-only body: `True`

#### Recognized Trading Calls
- none found

## Indicator Fields

- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.ATR[]` `cacheATR`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.EMA[]` `cacheEMA`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.MACD[]` `cacheMACD`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.MAX[]` `cacheMAX`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.MIN[]` `cacheMIN`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.OrderFlowCumulativeDelta[]` `cacheOrderFlowCumulativeDelta`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.OrderFlowMarketDepthMap[]` `cacheOrderFlowMarketDepthMap`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.OrderFlowTradeDetector[]` `cacheOrderFlowTradeDetector`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.OrderFlowVWAP[]` `cacheOrderFlowVWAP`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.RSI[]` `cacheRSI`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.SMA[]` `cacheSMA`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.Swing[]` `cacheSwing`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.VOL[]` `cacheVOL`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.VWAPNinjaBotIA[]` `cacheVWAPNinjaBotIA`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.WisemanAlligator[]` `cacheWisemanAlligator`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.WisemanAwesomeOscillator[]` `cacheWisemanAwesomeOscillator`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.WisemanFractal[]` `cacheWisemanFractal`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.WMA[]` `cacheWMA`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.WoodiesCCI[]` `cacheWoodiesCCI`
- `NinjaTrader.NinjaScript.Indicators.Indicator`: `NinjaTrader.NinjaScript.Indicators.WoodiesPivots[]` `cacheWoodiesPivots`
- `NinjaTrader.NinjaScript.Indicators.RSI`: `NinjaTrader.NinjaScript.Indicators.SMA` `smaDown`
- `NinjaTrader.NinjaScript.Indicators.RSI`: `NinjaTrader.NinjaScript.Indicators.SMA` `smaUp`
- `NinjaTrader.NinjaScript.MarketAnalyzerColumns.MarketAnalyzerColumn`: `NinjaTrader.NinjaScript.Indicators.Indicator` `indicator`
- `NinjaTrader.NinjaScript.Strategies.ema`: `NinjaTrader.NinjaScript.Indicators.Swing` `Swing1`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAMomentum_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.MACD` `macdI`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIARange_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.ATR` `atr`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIATrend_v1_0_0_1`: `NinjaTrader.NinjaScript.Indicators.SMA` `smaTR`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.EMA` `EMA1`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.EMA` `EMA2`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.RSI` `RSI1`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.SMA` `SMA1`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.SMA` `SMA2`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.ATR` `STR1`
- `NinjaTrader.NinjaScript.Strategies.NinjaBotIA.NinjaBotIAVolatility_v1_0_0_0`: `NinjaTrader.NinjaScript.Indicators.VWAPNinjaBotIA` `VWAP81`
- `NinjaTrader.NinjaScript.Strategies.Strategy`: `NinjaTrader.NinjaScript.Indicators.Indicator` `indicator`

