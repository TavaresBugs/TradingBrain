# Findings: NinjaBotIA.dll

Data: 2026-05-14

## Achado principal

O relatorio do `F - Static Strategy Flow Mapper` encontrou 6 classes que herdam de `Strategy`, mas todos os `OnStateChange` e `OnBarUpdate` dessas strategies aparecem com apenas 1 instrucao IL.

Inspecao direta confirmou:

```il
IL_0000: ret
```

Ou seja: na DLL carregada, os metodos principais da strategy estao vazios do ponto de vista estatico.

## Protecao detectada

O construtor estatico chama:

```csharp
<AgileDotNetRT>.Initialize();
<AgileDotNetRT>.PostInitialize();
```

Tambem existem recursos embutidos com nomes GUID e a secao `.text` tem alta entropia. Isso e consistente com payload protegido/empacotado.

## O que ainda temos de util

Mesmo com os corpos vazios, os metadados de propriedades e campos sobreviveram. Isso revela a arquitetura das estrategias:

### GoldBreakout

Campos/propriedades expostos:

- janela de horario: `StartHour`, `StartMinute`, `EndHour`, `EndMinute`, `CloseAllHour`, `CloseAllMinute`;
- risco: `Take1Ticks`, `Take2Ticks`, `StopLossTicks`;
- estado: `windowHigh`, `windowActive`, `tradesHoje`, `podeVender`, `ultimoLucro`, `loteAtual`;
- ideia provavel: rompimento de maxima de janela, parciais e stop fixo em ticks.

### Momentum

Campos/propriedades expostos:

- `Lb`;
- `MacdFast`, `MacdSlow`, `MacdSignal`;
- `StopLossTicks`, `ProfitTargetTicks`;
- series `upSeries`, `dnSeries`, `baseSeries`, `stateCh`;
- series `mg_*`, `ss_*`, `smma_*`, `tmp_*`;
- `macdI`;
- ideia provavel: momentum com MACD e alguma familia de medias/suavizacoes.

### Range

Campos/propriedades expostos:

- `avrng`, `acSeries`, `rfilt`, `filtDir`, `condIni`;
- `SwingPeriod`, `SwingMultiplier`;
- `FilteATR`, `ATRPeriod`, `ATRMultiplierTP`, `ATRMultiplierSL`;
- `atr`;
- ideia provavel: filtro de range com ATR, direcao de filtro e alvos/stops por ATR.

### Trend

Campos/propriedades expostos:

- `Periods`, `Multiplier`, `ChangeATR`;
- `TargetTicks`, `StopTicks`;
- `UseBreakEven`, `BreakEvenTriggerTicks`, `BreakEvenPlusTicks`;
- `UseTrailing`, `TrailTriggerTicks`, `TrailDistanceTicks`;
- `upSeries`, `dnSeries`, `trendSeries`, `trSeries`, `smaTR`;
- estado de BE/trailing: `beLongApplied`, `beShortApplied`, `curStopLong`, `curStopShort`;
- ideia provavel: SuperTrend/ATR trend follower com alvo, stop, break-even e trailing.

### Volatility

Campos/propriedades expostos:

- `EMA1Period`, `EMA2Period`;
- `RSIPeriod`, `RSISmooth`;
- `ATRPeriod`, `ATRSMAPeriod`;
- `SMAVolumePeriod`;
- `TrailStopMultiplier`, `BreakEvenMultiplier`;
- `TimeExitBars`, `MaxDrawdown`;
- indicadores: `EMA1`, `EMA2`, `RSI1`, `VWAP81`, `SMA1`, `SMA2`, `STR1`;
- estado: `longEntryBar`, `shortEntryBar`, `longEntryPrice`, `shortEntryPrice`, `highestProfit`;
- ideia provavel: tendencia por EMA/VWAP/RSI filtrada por ATR e volume, saida por trailing, break-even, tempo e drawdown.

## Proximo fruto pratico

Nao faz sentido insistir no `OnBarUpdate` estatico desta DLL porque ele esta vazio. O caminho produtivo agora e:

1. escolher uma strategy alvo;
2. reconstruir uma hipotese operacional com base nos campos expostos;
3. implementar essa hipotese em `TradingBrain.Core`;
4. rodar replay por CSV;
5. ajustar parametros ate o comportamento fazer sentido.

Comecei pela `NinjaBotIAVolatility_v1_0_0_0`, porque ela expoe os sinais mais claros: EMA, RSI, VWAP, ATR, volume, trailing, break-even, time exit e drawdown.
