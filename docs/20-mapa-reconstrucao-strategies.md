# NinjaBotIA reconstruction map

Data: 2026-05-14

## Estado atual

Temos duas coisas bem estabelecidas:

- A DLL `NinjaBotIA.dll` nao esta alinhada como build direta do arquivo `NinjaBotIA v3.cs` unificado.
- A DLL contem cinco estrategias separadas dentro de `NinjaTrader.NinjaScript.Strategies.NinjaBotIA`:
  - `NinjaBotIAGoldBreakout_v1_0_0_0`
  - `NinjaBotIAMomentum_v1_0_0_0`
  - `NinjaBotIARange_v1_0_0_0`
  - `NinjaBotIATrend_v1_0_0_1`
  - `NinjaBotIAVolatility_v1_0_0_0`

Tambem existe o indicador:

- `NinjaTrader.NinjaScript.Indicators.VWAPNinjaBotIA`

## Resultado do Agile

O probe rodado dentro do NinjaTrader confirmou que o runtime nativo do Agile carrega:

```text
C:\Program Files\NinjaTrader 8\bin\AgileDotNetRT64.dll
C:\Users\jhonv\AppData\Local\Temp\9e6c2f63-93e5-47f8-aed6-e82be50653e2\AgileDotNetRT64.dll
```

Mesmo apos forcar o `.cctor`, os metodos principais continuam vazios:

```text
OnStateChange after .cctor: len=1 il=2A
OnBarUpdate after .cctor:   len=1 il=2A
```

`2A` e `ret`. Entao a logica real nao foi restaurada como IL gerenciado normal. A rota de dump/decompilacao esta praticamente esgotada para `OnStateChange` e `OnBarUpdate`.

## O que temos

## Metadados de UI coletados pela V2

`NinjaBotIAConfigProbeV2` confirmou que a maioria dos parametros usa `DisplayAttribute`, `RangeAttribute` e `NinjaScriptPropertyAttribute`. Isso nos permite recriar a tela de configuracao do NinjaTrader com grupos, ordem e nomes proximos do original.

### Grupos principais

```text
1. Contracts
2. Time
3. Parameters
3. Parameters - Trend
3. Parameters - Volatility Filter
3. Risk Management
4. Risk Management
4. Exit
```

### Ranges padrao encontrados

```text
Horas: 0..23
Minutos/segundos: 0..59
Quantidades/periodos/ticks positivos: 1..int.MaxValue
Stops/targets opcionais no Momentum: 0..int.MaxValue
MaxDrawdown: 0..int.MaxValue
ATR multipliers: 0.1..double.MaxValue
Booleanos/doubles especificos sem RangeAttribute: sem limite declarado
```

### GoldBreakout

Propriedades publicas:

```text
LoteBase = 1
StartHour = 8
StartMinute = 30
EndHour = 10
EndMinute = 0
CloseAllHour = 16
CloseAllMinute = 55
UsePartial = false
Take1Ticks = 135
Take2Ticks = 295
StopLossTicks = 105
```

UI original aproximada:

```text
1. Contracts
  order 1: PositionSize -> LoteBase, range 1..int.MaxValue

2. Time
  order 2: Start Hour -> StartHour, range 0..23
  order 3: Start Minute -> StartMinute, range 0..59
  order 4: End Hour -> EndHour, range 0..23
  order 5: End Minute -> EndMinute, range 0..59
  order 6: Close All - Hour -> CloseAllHour, range 0..23
  order 7: Close All - Minute -> CloseAllMinute, range 0..59

3. Risk Management
  order 0: Use Partial -> UsePartial
  order 1: Target 1 (ticks) -> Take1Ticks, range 1..int.MaxValue
  order 2: Target 2 (ticks) -> Take2Ticks, range 1..int.MaxValue
  order 3: Stop (ticks) -> StopLossTicks, range 1..int.MaxValue
```

Campos privados:

```text
windowHigh = double.MinValue
windowActive = false
tradesHoje = 0
podeVender = false
ultimoLucro = 0
loteAtual = 0
MinimoLote = 1
StartHHmmss = 0
EndHHmmss = 0
CloseAllHHmmss = 0
```

Leitura provavel:

- Breakout por janela de preco.
- Controle de horario e fechamento forcado.
- Controle de lote dinamico ou minimo.
- Parcial com dois alvos e stop.
- Estado diario por `tradesHoje`.

### Momentum

Propriedades publicas:

```text
Quantity = 1
StartHour = 9
StartMinute = 0
EndHour = 11
EndMinute = 0
Lb = 14
MacdFast = 9
MacdSlow = 48
MacdSignal = 11
StopLossTicks = 295
ProfitTargetTicks = 320
```

UI original aproximada:

```text
1. Contracts
  order 0: PositionSize -> Quantity, range 1..int.MaxValue

2. Time
  order 1: Star tHour (0-23) -> StartHour, range 0..23
  order 2: Start Minute (0-59) -> StartMinute, range 0..59
  order 3: End Hour (0-23) -> EndHour, range 0..23
  order 4: End Minute (0-59) -> EndMinute, range 0..59

3. Parameters
  order 0: Lookback Length -> Lb, range 1..int.MaxValue
  order 0: MACD Fast -> MacdFast, range 1..int.MaxValue
  order 1: MACD Slow -> MacdSlow, range 1..int.MaxValue
  order 2: MACD Signal -> MacdSignal, range 1..int.MaxValue

4. Risk Management
  order 1: Stop Loss (ticks, 0=off) -> StopLossTicks, range 0..int.MaxValue
  order 2: Profit Target (ticks, 0=off) -> ProfitTargetTicks, range 0..int.MaxValue
```

Campos privados/publicos relevantes:

```text
upSeries
dnSeries
baseSeries
stateCh
mg_H, mg_L, mg_C
ss_H, ss_L, ss_C
smma_H, smma_L, smma_C
tmp_H, tmp_L, tmp_C
bullBrush
bearBrush
macdI
MAKind = "Smoothed"
ShowCloud = false
ShowArrows = false
UseTimeFilter = true
FlatWhenOutside = true
```

Helpers que sobraram como IL pequeno:

```text
ComputeMA
McGinley
SMMA
SuperSmoothed
NormalizeKind
Pick
InTimeWindow
ToHHmmss
```

Leitura provavel:

- Modulo de momentum com MACD.
- Filtro visual/estado com nuvem e setas.
- Calcula medias customizadas por high/low/close.
- `stateCh` provavelmente carrega estado direcional.
- `MAKind` escolhe SMA/EMA/WMA/McGinley/SMMA/SuperSmoothed ou similar.

### Range

Propriedades publicas:

```text
contractSize = 1
StartHour = 0
StartMinute = 0
EndHour = 12
EndMinute = 45
SwingPeriod = 15
SwingMultiplier = 4
FilteATR = 100
ATRPeriod = 12
ATRMultiplierTP = 3
ATRMultiplierSL = 3.5
```

UI original aproximada:

```text
1. Contracts
  order 1: PositionSize -> contractSize, range 1..int.MaxValue

2. Time
  order 1: Start Hour -> StartHour, range 0..23
  order 2: Start Minute -> StartMinute, range 0..59
  order 3: End Hour -> EndHour, range 0..23
  order 4: End Minute -> EndMinute, range 0..59

3. Parameters
  order 1: Swing Period -> SwingPeriod
  order 2: Swing Multiplier -> SwingMultiplier
  order 3: Filter ATR -> FilteATR

4. Risk Management
  order 1: ATR Period (TP/SL) -> ATRPeriod, range 1..int.MaxValue
  order 2: ATR Multiplier (TP) -> ATRMultiplierTP, range 0.1..double.MaxValue
  order 3: ATR Multiplier (SL) -> ATRMultiplierSL, range 0.1..double.MaxValue
```

Campos privados:

```text
avrng
acSeries
rfilt
filtDir
condIni
atr
```

Leitura provavel:

- Range filter com media de range (`avrng`).
- Filtro ativo `rfilt`.
- Direcao do filtro em `filtDir`.
- Condicao inicial em `condIni`.
- Saidas por ATR e swing.

### Trend

Propriedades publicas:

```text
contractSize = 1
StartHour = 9
StartMinute = 30
EndHour = 14
EndMinute = 30
Periods = 10
Multiplier = 2
ChangeATR = true
TargetTicks = 305
StopTicks = 250
UseBreakEven = true
BreakEvenTriggerTicks = 60
BreakEvenPlusTicks = 5
UseTrailing = true
TrailTriggerTicks = 80
TrailDistanceTicks = 100
```

UI original aproximada:

```text
1. Contracts
  order 1: PositionSize -> contractSize, range 1..int.MaxValue

2. Time
  order 1: Start Hour -> StartHour, range 0..23
  order 2: Start Minute -> StartMinute, range 0..59
  order 3: End Hour -> EndHour, range 0..23
  order 4: End Minute -> EndMinute, range 0..59

3. Parameters
  order 1: ATR Period (Trend Filter) -> Periods, range 1..int.MaxValue
  order 2: ATR Multiplier (Trend Filter) -> Multiplier, range 0.1..double.MaxValue
  order 3: Change ATR Calculation Method? -> ChangeATR

4. Risk Management
  order 1: Target (ticks) -> TargetTicks, range 1..int.MaxValue
  order 2: Stop (ticks) -> StopTicks, range 1..int.MaxValue
  order 3: Use BreakEven -> UseBreakEven
  order 4: BE Trigger (ticks) -> BreakEvenTriggerTicks, range 1..int.MaxValue
  order 5: BE Plus (ticks) -> BreakEvenPlusTicks, range 0..int.MaxValue
  order 6: Use Trailing -> UseTrailing
  order 7: Trail Trigger (ticks) -> TrailTriggerTicks, range 1..int.MaxValue
  order 8: Trail Distance (ticks) -> TrailDistanceTicks, range 1..int.MaxValue
```

Campos privados:

```text
upSeries
dnSeries
trendSeries
trSeries
smaTR
beLongApplied = false
beShortApplied = false
curStopLong = 0
curStopShort = 0
```

Leitura provavel:

- Estrutura de SuperTrend/ATR trend following.
- `trSeries` e `smaTR` indicam true range suavizado.
- `ChangeATR` alterna entre ATR nativo e SMA de true range.
- Gestao de alvo, stop, break-even e trailing.

### Volatility

Propriedades publicas:

```text
contractSize = 1
SessionStartHour = 9
SessionStartMinute = 30
SessionStartSecond = 0
SessionEndHour = 10
SessionEndMinute = 10
SessionEndSecond = 0
EMA1Period = 9
EMA2Period = 21
RSIPeriod = 14
RSISmooth = 3
ATRPeriod = 14
ATRSMAPeriod = 20
SMAVolumePeriod = 20
TrailStopMultiplier = 3.4
BreakEvenMultiplier = 0.4
TimeExitBars = 11
MaxDrawdown = 2
```

UI original aproximada:

```text
1. Contracts
  order 1: PositionSize -> contractSize, range 1..int.MaxValue

2. Time
  order 1: Start Hour -> SessionStartHour, range 0..23
  order 2: Start Minute -> SessionStartMinute, range 0..59
  order 3: Start Second -> SessionStartSecond, range 0..59
  order 4: End Hour -> SessionEndHour, range 0..23
  order 5: End Minute -> SessionEndMinute, range 0..59
  order 6: End Second -> SessionEndSecond, range 0..59

3. Parameters - Trend
  order 1: EMA1 Period -> EMA1Period, range 1..int.MaxValue
  order 2: EMA2 Period -> EMA2Period, range 1..int.MaxValue
  order 3: RSI Period -> RSIPeriod, range 1..int.MaxValue
  order 4: RSI Smooth -> RSISmooth, range 1..int.MaxValue
  order 5: ATR Period -> ATRPeriod, range 1..int.MaxValue

3. Parameters - Volatility Filter
  order 6: ATR SMA Period -> ATRSMAPeriod, range 1..int.MaxValue
  order 7: Volume SMA Period -> SMAVolumePeriod, range 1..int.MaxValue

4. Exit
  order 8: Trail-stop Multiplier -> TrailStopMultiplier
  order 9: Breakeven Multiplier -> BreakEvenMultiplier
  order 10: Time-exit Bars -> TimeExitBars, range 1..int.MaxValue
  order 11: Max Drawdown -> MaxDrawdown, range 0..int.MaxValue
```

Campos privados:

```text
EMA1
EMA2
RSI1
VWAP81
SMA1
SMA2
STR1
volatilityFilter
ATR1 = 0
trailStop = 0
breakEvenTrigger = 0
longEntryBar = -1
shortEntryBar = -1
longEntryPrice = 0
shortEntryPrice = 0
maxDrawdownPercent = 0
highestProfit = 0
```

Leitura provavel:

- Filtro de volatilidade por ATR e media de ATR.
- Filtro de volume por SMA de volume.
- Direcao por EMA1/EMA2.
- Confirmacao por RSI e VWAP.
- Gestao por trailing, break-even, saida por tempo e drawdown maximo.

## O que falta para reconstruir de forma fiel

### Defaults reais

Coletado com `NinjaBotIAConfigProbe`.

Status: resolvido para as propriedades publicas principais.

Ainda falta confirmar se existem defaults adicionais em propriedades publicas nao declaradas diretamente ou parametros herdados que a estrategia alterava em `OnStateChange`, porque `OnStateChange` esta protegido e nao executa logica visivel.

### Atributos de propriedades

O probe confirmou que as propriedades principais usam:

```text
NinjaScriptProperty
Display(Name, GroupName, Order)
Range(...)
```

O relatorio atual nao expandiu os argumentos de `DisplayAttribute` e `RangeAttribute`. Isso ainda pode ser minerado com uma versao mais detalhada do probe, se precisarmos de nomes exibidos, ordem e limites exatos da UI.

### Regras exatas de entrada e saida

O IL nao revelou:

```text
condicoes de entrada long/short
condicoes de saida
ordem de avaliacao dos filtros
nomes dos sinais
uso exato de EnterLong/EnterShort/Exit*
```

Isso precisa ser reconstruido por comportamento.

### Comportamento em backtest

Precisamos rodar cada modulo em Strategy Analyzer com poucos cenarios controlados e observar:

```text
quando entra
quando sai
se usa stop/target fixo
se altera stop dinamicamente
se fecha fora do horario
se inverte posicao ou espera flat
nomes dos sinais/ordens no executions
```

## Proximo probe

`NinjaBotIAConfigProbe` criado e rodado.

Objetivos:

- Instanciar cada tipo real da DLL: feito.
- Listar propriedades declaradas: feito.
- Ler valores atuais/defaults: feito.
- Listar atributos de cada propriedade: parcialmente feito.
- Listar campos privados e valores simples: feito.
- Salvar um relatorio em:

```text
C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\log\NinjaBotIAConfigProbe-report.txt
```

`NinjaBotIAConfigProbeV2` rodado e consolidado no mapa. Ele expandiu:

```text
DisplayAttribute.Name
DisplayAttribute.ShortName
DisplayAttribute.Description
DisplayAttribute.GroupName
DisplayAttribute.Order
RangeAttribute.Minimum
RangeAttribute.Maximum
lista completa de atributos por propriedade
```

Relatorio esperado:

```text
C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\log\NinjaBotIAConfigProbeV2-report.txt
```

## Plano de reconstrucao

1. Completar o mapa de defaults e atributos com `NinjaBotIAConfigProbe`.
2. Atualizar o esqueleto `NinjaBotIA_dll_shape.cs` com defaults e metadados visiveis.
3. Reconstruir modulo por modulo, nesta ordem:
   - GoldBreakout: mais simples e mais parametrico.
   - Trend: padrao SuperTrend/ATR e gestao clara.
   - Range: range filter com ATR.
   - Volatility: mais rico, mas campos indicam arquitetura.
   - Momentum: depende de medias customizadas e estado, provavelmente o mais chato.
4. Validar cada modulo em Strategy Analyzer com cenarios pequenos.
5. Comparar entradas/saidas da DLL original contra a reconstrucao.

## Probe de comportamento

Criado o `NinjaBotIABehaviorTraceProbe`, com foco no que ainda falta: comportamento real de ordens, execucoes e posicoes.

Arquivos:

```text
C:\Users\jhonv\Documents\Codex\2026-05-14\estavamos-em-uma-conversa-anterior-019e26ae\NinjaBotIABehaviorTraceProbe.cs
C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\bin\Custom\Strategies\NinjaBotIABehaviorTraceProbe.cs
```

Validacao local:

```text
Compilacao C# standalone contra NinjaTrader.Core.dll/NinjaTrader.Gui.dll/NinjaTrader.Custom.dll: OK.
```

Saida gerada pelo probe quando rodar no NinjaTrader:

```text
C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\log\NinjaBotIABehaviorTraceProbe-events.csv
```

O CSV registra:

```text
ORDER
EXECUTION
POSITION
account
instrument
orderName
entrySignal
oco
action
type
state
quantity/fill/precos
orderId/executionId
```

Uso recomendado:

```text
1. Compilar no NinjaScript Editor.
2. Rodar em Sim101/Playback, nunca em conta real.
3. Adicionar o NinjaBotIABehaviorTraceProbe em um chart.
4. Rodar um modulo original da DLL no mesmo ambiente.
5. Repetir por modulo: GoldBreakout, Trend, Range, Volatility, Momentum.
6. Comparar o CSV gerado com os trades do Strategy Analyzer/Executions.
```

Filtros disponiveis no probe:

```text
AccountNameFilter
InstrumentFilter
NameContainsFilter
LogPositions
LogOnlyNinjaBotLikeNames
```

Observacao importante: este probe captura eventos de conta em Sim/Playback/execucao conectada. Para Strategy Analyzer puro, o caminho principal continua sendo exportar/listar os trades do analyzer e comparar contra a reconstrucao.
