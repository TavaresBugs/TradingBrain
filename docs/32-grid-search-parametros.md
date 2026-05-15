# Grid Search Notes

Foi adicionada uma camada de parametros (`StrategyTuningParams`) para evitar thresholds fixos dentro das regras refinadas.

## Arquivos alterados

- `src/TradingBrain.Core/BacktestModels.cs`
  - Adiciona `StrategyTuningParams`.
  - Adiciona `GridSearchResult`.

- `src/TradingBrain.Console/StrategyBacktester.cs`
  - Recebe parametros opcionais no construtor.
  - Usa parametros em `EvaluateVolatility`, `EvaluateRange`, `EvaluateMomentum`, `EvaluateEma`, `EvaluateTrend` e `EvaluateOrbBreakout`.

- `src/TradingBrain.Console/GridSearchRunner.cs`
  - Novo runner com grids pequenos por estrategia.
  - Exporta `*.grid.csv`.

- `src/TradingBrain.Console/Program.cs`
  - Novo comando `--grid-search`.

## Comandos

Grid para uma estrategia:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --grid-search "C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt" .\outputs\grid-search-output Momentum
```

Grid para estrategias principais:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --grid-search "C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt" .\outputs\grid-search-output
```

## Observacao

A primeira execucao de grid no MNQ foi interrompida manualmente antes de concluir. O codigo compila, mas os resultados de grid ainda devem ser gerados em uma rodada completa.
