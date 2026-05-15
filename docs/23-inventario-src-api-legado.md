# Inventario de src: API atual e legado

Data base: 2026-05-15

Este documento registra a leitura estrutural da pasta `src` ao fechar a Fase 1. Ele nao muda codigo; apenas separa o que o backtester atual usa do que parece legado preservado para revisao posterior.

## Regra da Fase 1

Nenhum arquivo de codigo foi movido ou removido nesta fase. A ideia e evitar quebrar o projeto enquanto ainda estamos consolidando a base documental.

## API atual do playground

Arquivos que fazem parte do fluxo principal atual:

| Arquivo | Papel |
|---|---|
| `src\TradingBrain.Core\BacktestModels.cs` | Modelos do backtester: decisoes, linhas, trades, resumo, parametros e resultados de grid. |
| `src\TradingBrain.Core\MarketBar.cs` | Modelo basico de candle OHLCV. |
| `src\TradingBrain.Core\SignalAction.cs` | Enum de acoes de sinal. |
| `src\TradingBrain.Core\StrategyKind.cs` | Enum das strategies reconstruidas. |
| `src\TradingBrain.Core\TechnicalIndicators.cs` | Indicadores puros usados pelo runner. |
| `src\TradingBrain.Console\Program.cs` | CLI e roteamento dos comandos. |
| `src\TradingBrain.Console\StrategyBacktester.cs` | Loop principal de backtest e controle de posicao. |
| `src\TradingBrain.Console\StrategyRules.cs` | Regras `Evaluate...` das strategies reconstruidas. |
| `src\TradingBrain.Console\BacktestReports.cs` | Extracao de trades, summaries e export CSV. |
| `src\TradingBrain.Console\GridSearchRunner.cs` | Grid search e ranking de parametros. |
| `src\TradingBrain.Console\CsvBarReader.cs` | Parser de CSV. |
| `src\TradingBrain.Console\DllMetadataInspector.cs` | Inspecao estatica read-only da DLL. |
| `src\TradingBrain.Console\CleanNinjaScriptGenerator.cs` | Geracao do NinjaScript limpo. |

## Legado preservado

Arquivos que parecem pertencer ao primeiro prototipo de motor isolado/adaptador e nao aparecem no fluxo principal do backtester atual:

| Arquivo | Observacao |
|---|---|
| `src\TradingBrain.Core\DecisionInput.cs` | Entrada simples usada por `DecisionEngine`. |
| `src\TradingBrain.Core\DecisionResult.cs` | Resultado simples usado por motores antigos. |
| `src\TradingBrain.Core\IndicatorState.cs` | Estado incremental simples de medias/RSI; nao e usado pelo runner atual. |
| `src\TradingBrain.Core\DecisionEngine.cs` | Motor didatico inicial usado pelo exemplo em `adapters`. |
| `src\TradingBrain.Core\VolatilityDecisionConfig.cs` | Config antiga do motor Volatility isolado. |
| `src\TradingBrain.Core\VolatilityDecisionEngine.cs` | Motor Volatility isolado anterior ao backtester multi-strategy atual. |
| `src\TradingBrain.Core\VolatilityDecisionInput.cs` | Entrada antiga do motor Volatility isolado. |

## Recomendacao para fase posterior

Na fase de qualidade de codigo, escolher um destes caminhos:

1. Promover o motor isolado antigo para API oficial do adaptador NinjaTrader.
2. Migrar o adaptador para usar o mesmo fluxo multi-strategy do backtester.
3. Arquivar/remover os arquivos legados se eles nao forem mais necessarios.

Decisao recomendada: manter por enquanto, porque o adaptador ainda referencia `DecisionEngine`. A remocao deve acontecer apenas quando existir um adaptador novo usando a arquitetura atual.
