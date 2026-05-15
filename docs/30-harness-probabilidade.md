# Probability Harness Notes

Este arquivo resume o que foi adicionado ao playground para facilitar analise financeira externa.

## Objetivo

Transformar o backtester em um harness de probabilidade: alem do CSV por candle, agora ele exporta trades fechados e metricas estatisticas por estrategia.

## Comando principal

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --run-all "C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt" .\outputs\probability-harness-output
```

## Arquivos gerados

- `strategy-summary.csv`: comparativo das estrategias.
- `volatility.signals.csv`, `trend.signals.csv`, etc.: linha por candle.
- `volatility.trades.csv`, `trend.trades.csv`, etc.: linha por trade fechado.

## Campos importantes em `*.trades.csv`

- `Direction`: Long ou Short.
- `EntryTime` / `ExitTime`.
- `EntryPrice` / `ExitPrice`.
- `BarsHeld`.
- `PnL`: lucro/prejuizo bruto do trade em pontos, mantido por compatibilidade.
- `GrossPoints` / `GrossCurrency`: resultado antes de custos.
- `NetPoints` / `NetCurrency`: resultado depois de slippage, spread e comissao.
- `TotalCostCurrency`: custo total do round-trip.
- `SlippageCostCurrency`, `SpreadCostCurrency`, `CommissionCostCurrency`: decomposicao dos custos.
- `MFE`: max favorable excursion, melhor lucro aberto durante o trade.
- `MAE`: max adverse excursion, pior perda aberta durante o trade.
- `EntryReason` / `ExitReason`.

## Campos importantes em `strategy-summary.csv`

- `WinRate`: percentual de trades positivos.
- `ProfitFactor`: lucro bruto dividido pelo prejuizo bruto absoluto.
- `AverageWin`: media dos trades vencedores.
- `AverageLoss`: media dos trades perdedores.
- `PayoffRatio`: `AverageWin / abs(AverageLoss)`.
- `Expectancy`: expectativa media por trade.
- `GrossPnL`: soma dos pontos brutos.
- `TotalCosts`: soma dos custos em moeda.
- `NetPnL`: soma dos pontos liquidos.
- `NetProfitFactor`: profit factor depois dos custos.
- `NetExpectancy`: expectativa media por trade depois dos custos.
- `GrossCurrency` / `NetCurrency`: resultado financeiro antes/depois dos custos.
- `TradeStdDev`: desvio padrao dos PnLs por trade.
- `ReturnToDrawdown`: `NetPnL / MaxDrawdown`.

## Custos de execucao

A Fase 2 adicionou `ExecutionSettings` com defaults MNQ:

```text
TickSize = 0.25
TickValue = 0.50
SlippageTicks = 1
SpreadTicks = 1
CommissionPerSide = 0
Quantity = 1
```

Exemplo com comissao:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --run-all "C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt" .\outputs\probability-harness-output --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62 --quantity 1
```

Os sinais e o numero de trades nao mudam quando os custos mudam. Apenas as metricas liquidas mudam.

## Resultado MNQ inicial

Rodada com `MNQ 06-26.Last.txt`, 13471 barras:

| Strategy | Trades | WinRate | ProfitFactor | Expectancy | NetPnL | MaxDD |
|---|---:|---:|---:|---:|---:|---:|
| Volatility | 18 | 38.89% | 1.4223 | 3.0972 | 55.75 | 108.75 |
| GoldBreakout | 0 | 0% | 0 | 0 | 0 | 0 |
| Momentum | 74 | 39.19% | 1.4835 | 2.6791 | 198.25 | 151.50 |
| Range | 7 | 42.86% | 1.4007 | 7.6429 | 53.50 | 133.50 |
| Trend | 24 | 41.67% | 0.6903 | -8.2188 | -197.25 | 461.00 |
| Ema | 227 | 44.49% | 1.1574 | 2.2104 | 501.75 | 422.75 |

## Observacoes

- O runner ainda usa entrada/saida no fechamento do candle, mas agora desconta slippage, spread e comissao no resultado dos trades.
- A performance atual e suficiente para analise pontual; a rodada completa no MNQ levou alguns minutos porque varios indicadores ainda sao recalculados de forma simples.
- O proximo passo natural e criar manifesto de rodada e depois split treino/teste/walk-forward.
