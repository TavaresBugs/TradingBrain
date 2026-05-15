# Automation plan: reconstruir a estrategia mais fiel

## 1. Como saber qual estrategia esta sendo testada

O runner deve sempre receber uma estrategia explicitamente:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- C:\dados\MNQ.txt C:\dados\sinais.csv --strategy Volatility
```

O CSV exportado inclui a coluna `Strategy`, por exemplo:

```text
NinjaBotIAVolatility_v1_0_0_0
```

Assim nao existe ambiguidade entre `GoldBreakout`, `Momentum`, `Range`, `Trend`, `Volatility` e `ema`.

## 2. O que significa "mais fiel"

Ha duas metricas diferentes:

- `performance`: lucro, drawdown, win rate, profit factor.
- `fidelidade`: o quanto os sinais reconstruidos batem com os sinais da strategy original.

Sem uma referencia da strategy original, so conseguimos otimizar performance. Para medir fidelidade, precisamos de um arquivo de referencia com sinais/execucoes reais da strategy original.

Formato sugerido:

```csv
Time,Signal,Price
2026-05-01 09:04:00,Sell,27547.75
2026-05-01 09:23:00,Exit,27565
```

Com isso, o otimizador consegue calcular:

- entradas coincidentes;
- saidas coincidentes;
- sinais faltantes;
- sinais extras;
- diferenca media em barras;
- score de fidelidade.

## 3. Automacao proposta

### Etapa A: fingerprint dos metadados

O plugin gera os campos expostos de cada strategy.

Exemplo:

```text
Volatility: EMA, RSI, VWAP, ATR, SMA volume, trailing, breakeven, time exit, max drawdown
Trend: ATR/SuperTrend, break-even, trailing, target/stop ticks
Range: range filter, ATR, swing, TP/SL por ATR
Momentum: MACD, medias suavizadas, cloud/arrows
GoldBreakout: janela, windowHigh, parcial, targets/stop fixos
```

### Etapa B: escolher motor

O runner seleciona o motor por `--strategy`.

Hoje implementado:

```text
Volatility
```

Proximos motores:

```text
Trend
Range
Momentum
GoldBreakout
Ema
```

### Etapa C: grid search

O grid search varia parametros plausiveis:

```text
EMA1Period
EMA2Period
RSIPeriod
ATRSMAPeriod
SMAVolumePeriod
VolumeThreshold
TrailStopMultiplier
BreakEvenMultiplier
TimeExitBars
SessionStart/End
```

Se nao houver referencia original, ordena por:

```text
NetPnL alto + MaxDrawdown baixo + numero de trades razoavel
```

Se houver referencia original, ordena por:

```text
FidelityScore primeiro, performance depois
```

### Etapa D: gerar codigo NinjaTrader limpo

Quando um conjunto de regras fica estavel, gerar:

```text
NinjaBotIAVolatility_Reconstructed.cs
```

Esse arquivo deve conter:

- propriedades com os nomes do dnSpy;
- `OnStateChange` inicializando indicadores;
- `OnBarUpdate` chamando o motor reconstruido;
- logs opcionais de diagnostico;
- sem dependencia da DLL protegida.

## 4. Proximo passo recomendado

Para encontrar o padrao mais fiel:

1. exportar uma lista de sinais/execucoes da strategy original, se ela aparecer no grafico ou em account executions;
2. criar o `FidelityScorer`;
3. rodar grid search contra MNQ;
4. gerar o `.cs` NinjaTrader da melhor configuracao.

Sem o arquivo de referencia, ainda da para otimizar uma strategy lucrativa, mas nao da para afirmar matematicamente que ela e a mais fiel a original.
