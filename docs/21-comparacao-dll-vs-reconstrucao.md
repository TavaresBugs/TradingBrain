# Locked DLL vs reconstructed code

Data: 2026-05-14

## Estado da comparacao

O `NinjaBotIABehaviorTraceProbe-events.csv` existe, mas contem apenas:

```text
TRACE_START
SUBSCRIBE
TRACE_STOP
```

Nao ha eventos `ORDER`, `EXECUTION` ou `POSITION`. Portanto, ainda nao existe uma amostra operacional do codigo protegido para comparar trade a trade.

O que ja conseguimos comparar com seguranca:

- classes reais presentes na DLL;
- propriedades publicas;
- defaults reais;
- ranges/nomes de UI;
- campos privados;
- perfil esperado de cada estrategia;
- resultado do nosso codigo reconstruido no MNQ.

O que ainda falta para comparar "lado a lado de verdade":

- export de execucoes/sinais da estrategia original rodando em Playback/Sim101;
- ou export de trades do Strategy Analyzer da strategy original.

## Side-by-side atual

| Strategy | Locked DLL: evidencia real | Reconstructed: status | Reconstructed MNQ result |
|---|---|---|---|
| Volatility | Defaults e campos confirmados. `OnBarUpdate = ret`. Campos: EMA, RSI, VWAP, ATR, SMA volume, trailing, breakeven, time exit, drawdown. | Motor heuristico alinhado aos defaults reais. | 18 trades, Net +55.75, DD 108.75 |
| Momentum | Defaults confirmados. Campos: MACD, `Lb`, medias customizadas, cloud/arrows, time filter, flat outside. | Motor heuristico por MACD/RSI/EMA e janela real. | 74 trades, Net +198.25, DD 151.50 |
| Range | Defaults confirmados. Campos: range filter, ATR, swing, TP/SL ATR. | Motor heuristico por range filter + ATR. | 7 trades, Net +53.50, DD 133.50 |
| Trend | Defaults confirmados. Campos: SuperTrend-like ATR, target, stop, BE, trailing. | Motor heuristico por SuperTrend/ATR. | 24 trades, Net -197.25, DD 461.00 |
| GoldBreakout | Defaults confirmados. Campos: windowHigh, windowActive, parcial, targets, stop, close all. | Motor heuristico por rompimento de janela. | 0 trades no MNQ atual |
| Ema | Campos confirmados: Swing, Stop, Target. Defaults ainda menos ricos que os outros. | Motor heuristico EMA + swing + stop/target. | 227 trades, Net +501.75, DD 422.75 |

## Como obter comparacao trade a trade

### Opcao A: BehaviorTraceProbe em Playback/Sim101

1. Abrir NinjaTrader.
2. Compilar `NinjaBotIABehaviorTraceProbe`.
3. Adicionar o probe em um chart.
4. Adicionar a strategy original protegida no mesmo Playback/Sim101.
5. Usar o mesmo instrumento e periodo do arquivo MNQ.
6. Rodar a reproducao.
7. Conferir se este arquivo passa a conter `ORDER` e `EXECUTION`:

```text
C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\log\NinjaBotIABehaviorTraceProbe-events.csv
```

### Opcao B: Strategy Analyzer

1. Rodar a strategy original no Strategy Analyzer.
2. Exportar/listar os trades.
3. Salvar em CSV com colunas:

```csv
Strategy,EntryTime,EntryAction,EntryPrice,ExitTime,ExitPrice,Quantity,Profit
```

## Comparacao automatica planejada

Quando houver CSV original, gerar:

```text
locked-vs-reconstructed-trades.csv
```

Com colunas:

```text
Strategy
OriginalEntryTime
ReconstructedEntryTime
EntryDeltaBars
OriginalDirection
ReconstructedDirection
OriginalEntryPrice
ReconstructedEntryPrice
OriginalExitTime
ReconstructedExitTime
ExitDeltaBars
OriginalPnL
ReconstructedPnL
MatchScore
```

Isso permite medir fidelidade, nao apenas performance.
