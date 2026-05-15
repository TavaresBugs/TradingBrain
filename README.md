# TradingBrain

Playground para reconstruir, testar e comparar estrategias NinjaTrader sem depender da DLL vendor bloqueada.

## Quick Start

```bash
git clone <repo-url> tradingbrain && cd tradingbrain
dotnet build ./TradingBrain.slnx
dotnet run --project ./src/TradingBrain.Console/TradingBrain.Console.csproj -- ./src/TradingBrain.Console/sample-bars.csv ./outputs/quickstart/volatility.signals.csv --strategy Volatility
```

## Estrutura

- `src/TradingBrain.Core`: modelos, indicadores e tipos compartilhados.
- `src/TradingBrain.Console`: comandos de replay, grid search, inspecao estatica e geracao de NinjaScript limpo.
- `tests/TradingBrain.Tests`: testes unitarios de indicadores, parser CSV e motores de decisao.
- `adapters`: exemplo de adaptador fino para usar o motor dentro da estrategia oficial.
- `docs`: documentacao tecnica organizada. Comece por `docs\README.md`.
- `outputs`: resultados gerados, separados do codigo.
- `generated`: NinjaScript reconstruido em clean-room, gerado localmente e ignorado pelo Git.
- `probes`: ferramentas e registros historicos de diagnostico.

## Documentos Principais

| Arquivo | Papel |
|---|---|
| `PROJECT_STATUS.md` | Foto curta do estado atual: funcionalidades, limitacoes e arquivos de entrada. |
| `ROADMAP.md` | Plano de evolucao por fases, prioridades e criterios de pronto. |
| `docs\README.md` | Indice navegavel da documentacao tecnica. |
| `docs\00-visao-geral-e-espinha-dorsal.md` | Fonte principal da verdade tecnica. |
| `docs\33-volatility-v2-spec.md` | Mapeamento da especificacao Volatility v2 implementada. |

## Como Usar

Abra `TradingBrain.slnx` no Visual Studio ou rode pelo terminal.

```powershell
# Backtest simples
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- .\src\TradingBrain.Console\sample-bars.csv .\outputs\sample-volatility-output.csv --strategy Volatility

# Build e testes
dotnet build .\TradingBrain.slnx
dotnet test .\TradingBrain.slnx

# Backtest com custos MNQ explicitos
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- .\src\TradingBrain.Console\sample-bars.csv .\outputs\sample-volatility-output.csv --strategy Volatility --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62 --quantity 1

# Executar todas as estrategias
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --run-all "C:\caminho\dados.csv" .\outputs\all-strategies

# Grid search
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --grid-search "C:\caminho\dados.csv" .\outputs\grid-search Momentum

# Gerar NinjaScript limpo
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --generate-ninja .\generated

# Inspecionar a DLL bloqueada, somente metadados
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --inspect-dll "C:\caminho\NinjaBotIA.dll" .\docs\10-evidencia-dll-inspecao-estatica.md
```

## Formato de Entrada

CSV padrao:

```csv
time,open,high,low,close,volume
2026-01-02 09:00:00,100.00,101.00,99.50,100.00,1200
```

Formato NinjaTrader tambem aceito:

```text
yyyyMMdd HHmmss;open;high;low;close;volume
```

## Saidas Geradas

- `*.signals.csv`: uma linha por candle com indicadores, sinal, equity e drawdown.
- `*.trades.csv`: trades fechados com entrada, saida, PnL bruto, PnL liquido, custos, MFE e MAE.
- `strategy-summary.csv`: resumo comparativo por estrategia com metricas brutas e liquidas.
- `*.grid.csv`: combinacoes de parametros com score, win rate, profit factor, expectancy, custos e drawdown.
- `run-manifest.json`: manifesto da rodada com dataset, data/hora, strategy, parametros, custos, versao do codigo e arquivos gerados.

## Custos de Execucao

As flags abaixo sao opcionais e usam defaults MNQ quando omitidas:

```powershell
--tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0 --quantity 1
```

- `GrossPoints` / `GrossCurrency`: resultado antes de custos.
- `NetPoints` / `NetCurrency`: resultado depois de slippage, spread e comissao.
- `TotalCostCurrency`: custo total do round-trip.

## Regras do Projeto

- Estrategia nao deve depender da DLL bloqueada.
- Toda logica nova deve ser testavel pelo playground.
- Evidencia da DLL, hipotese reconstruida e resultado empirico devem ficar separados.
- Mantenha `bin` e `obj` fora do pacote final.
