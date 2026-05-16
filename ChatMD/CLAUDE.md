# TradingBrain — Project Context & Agent Rules

> Mantenha este arquivo atualizado a cada sessão concluída.  
> Lido automaticamente pelo Claude Code. Para o chat, cole: "leia o CLAUDE.md antes de continuar."

---

## O que é o projeto

Engine de backtest próprio em C# (.NET 10) para MNQ (Micro Nasdaq Futures). Reconstrói, valida e refina strategies que originalmente existiam em uma DLL proprietária do NinjaTrader. O pipeline é completamente independente — roda sobre CSVs de barras OHLCV, sem NinjaTrader nem DLL externa.

Repositório: https://github.com/TavaresBugs/TradingBrain

---

## Stack & ambiente

- **Runtime:** .NET 10 (Ubuntu, X11)
- **Build:** `dotnet build ./TradingBrain.slnx`
- **Testes:** `dotnet test ./TradingBrain.slnx --verbosity normal`
- **Run:** `dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- <args>`

### Parâmetros de execução padrão (MNQ — sempre usar)

```
--tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### Comandos disponíveis

```bash
dotnet run -- <bars.csv> <output_dir> [--strategy <nome>]
dotnet run -- --grid-search <bars.csv> <output_dir> [<Strategy>] [--no-regime-filter]
dotnet run -- --walk-forward <bars.csv> <output_dir> <Strategy> --windows N [--no-regime-filter]
dotnet run -- --classify-regime <bars.csv> <output_dir>
dotnet run -- --run-all <bars.csv> <output_dir>
dotnet run -- --replay <signals.csv> [--delay 200]
```

---

## Dados

**Dataset principal:** `outputs/tv-bars/mnq_5m_12mo.csv` (não versionado)

- Período: 2025-03-16 → 2026-05-15 (~14 meses)
- 82.659 barras de 5 minutos, formato `time,open,high,low,close,volume` (horário ET, sem timezone)
- Origem: 4 arquivos do TradingView exportados manualmente e merged

**Script de conversão TradingView → TradingBrain:**
```bash
python3 adapters/TradingView/convert_tradingview.py input.csv output.csv --drop-zero-volume
```

---

## Estado atual das implementações

### ✅ Concluído

- **10 strategies implementadas:** Volatility, Momentum, Ema, Trend, Range, OrbBreakout, VwapReversion, BollingerFade, SchoolRun, IbBreakout
- **Pipeline completo:** grid search IS/OOS (65/35), walk-forward rolling, classify-regime, run-all, replay
- **PrecomputedSeries:** indicadores O(n) — elimina recálculo no grid search
- **IB Classifier:** substituiu o KER-based. Classifica cada dia por Initial Balance (9:30-10:30 ET). Regimes: Trend, Breakout, Range, HighVolatility, NonTrend, Undefined
- **RegimeFilter:** filtra o dataset por regime alvo antes do grid search. `StrategyRegimeMap` define o mapeamento fixo strategy → regime. Flag `--no-regime-filter` para comparação
- **WalkForward com regime filter:** filtro aplicado antes do split em janelas. Gate de 30 dias mínimos após filtro
- **Strategy `IbBreakout` canônica:** range 9:30-10:30 direto das barras de 5min, sem resample. Smoke test roda, mas grid/walk-forward filtrados ainda não encontram trades suficientes nos regimes Breakout+Trend.

### 🔜 Próximos passos (em ordem de prioridade)

1. **Validação estatística do IB classifier** nos 82k dados reais — confirmar que `directionality` de dias Trend > Range após o novo classifier (era o inverso com KER)
2. **Validar/tunar `IbBreakout`** — default gerou 5 trades no dataset completo, mas 0 resultados no grid filtrado Breakout+Trend; revisar thresholds ou regime alvo antes de considerar produção.
3. **Mais dados** — baixar 2024 do TradingView (4 arquivos) para ter 2+ anos e validar IB com significância
4. **WalkForward com regime** — comparar resultados SchoolRun com e sem filtro (baseline: 1/5 janelas OOS positivo sem filtro)
5. **SchoolRun anti-mode** — testar `UseAntiMode=True` em dias onde preço está dentro do overnight range
6. **OrbBreakout grid expandido** — adicionar variação da janela de formação (9:30-10:00 vs 9:30-10:30)

---

## Arquitetura — estrutura relevante

```
src/TradingBrain.Core/
  MarketBar.cs              # OHLCV + timestamp
  PrecomputedSeries.cs      # Indicadores pré-computados por índice
  TechnicalIndicators.cs    # EMA, ATR, MACD, RSI, Resample, KER, VWAP...
  RegimeClassifier.cs       # IB-based classifier (sinal primário: Initial Balance)
  MarketRegime.cs           # Enum MarketRegime + record DayRegime (com campos IB)
  RegimeFilter.cs           # Apply() — filtra barras por regime alvo
  StrategyRegimeMap.cs      # Mapeamento fixo StrategyKind → []MarketRegime
  BacktestModels.cs         # StrategyTuningParams, BacktestSummary, GridSearchResult...
  ExecutionSettings.cs      # tick, slippage, comissão
  StrategyKind.cs           # Enum de strategies
  DecisionEngine.cs / VolatilityDecisionEngine.cs

src/TradingBrain.Console/
  StrategyBacktester.cs     # Loop principal (partial class)
  StrategyRules.cs          # Lógica entrada/saída por strategy (partial class)
  GridSearchRunner.cs       # Grid IS/OOS com filtro de regime
  WalkForwardValidator.cs   # Walk-forward rolling com filtro de regime
  BacktestReports.cs        # Métricas, CSVs, summarize
  ReplayViewer.cs           # Viewer barra a barra (--replay)
  Program.cs                # Entry point, parsing de comandos
```

---

## Regras para o agente

### Sobre modificações de código

- **Nunca quebre assinaturas públicas existentes.** Novos parâmetros sempre opcionais com valor default.
- **Nunca remova** valores de enum existentes — apenas adicione.
- **Nunca remova funcionalidade existente** — adicione e substitua apenas o que está especificado.
- Ao modificar um `record`, adicione novos campos ao final com valores default para compatibilidade.

### Sobre testes

- Os testes existentes devem continuar passando após qualquer modificação. Se quebrarem, corrija sem alterar lógica de negócio.
- Novos testes sempre em arquivo separado (ex: `FeatureNameTests.cs`).
- Ao concluir a implementação, confirme a contagem total de testes no output do `dotnet test`.

### Sobre regime e lookahead

- **A classificação de regime sempre usa o dataset completo** antes de qualquer split IS/OOS ou janela de walk-forward. Isso garante que ATR14 e IB de ontem tenham contexto suficiente.
- **Ordem correta obrigatória:**
  1. Classifica regime em `allBars` completo
  2. Filtra `allBars` por regime alvo → `filteredBars`
  3. Divide `filteredBars` em janelas cronológicas
  4. Para cada janela: IS = primeiros 65%, OOS = últimos 35%
- **Gate mínimo:** se após o filtro de regime restarem menos de 30 dias, não rodar o backtest — retornar vazio e logar o motivo.
- `NonTrend` é **sempre excluído** de todas as strategies, mesmo que esteja em `allowedRegimes`.

### Sobre outputs e diagnósticos

- Ao aplicar filtro de regime, sempre logar: `[RegimeFilter] Strategy: X/Y dias úteis (Regime)`.
- No início de `--grid-search`, imprimir a distribuição completa de regimes do dataset.
- Novos campos em modelos de resultado devem aparecer nas colunas do CSV de saída.

---

## Decisões de design tomadas

| Decisão | Motivo |
|---|---|
| Engine próprio em C# | Independência da DLL proprietária NinjaTrader |
| CSV como input | Portabilidade, sem dependência de broker |
| PrecomputedSeries | O(n) em vez de O(n²) — crítico no grid search |
| IS/OOS gate: NetPnL > 0 no OOS | Score com NetExpectancy pode ser positivo com PnL negativo em amostras pequenas |
| IB como classificador principal | KER mede o passado (tardio). IB mede o comportamento da sessão atual — metodologia institucional real (Market Profile, CBOT) |
| Classificação em allBars antes do split | Evita lookahead sutil: ATR14 de janelas pequenas não tem contexto suficiente para os primeiros dias |
| Walk-forward com filtro de regime | Walk-forward sequencial penaliza strategies de regime injustamente — penaliza SchoolRun por operar em dias de Range onde não tem edge |
| `NonTrend` sempre excluído | Dias sem direção nem volatilidade não têm edge para nenhuma strategy |

---

## Score do grid search

```
Gate: ClosedTrades >= 30 E NetExpectancy > 0

pf         = Min(NetProfitFactor, 10.0)
rtd        = Clamp(ReturnToDrawdown, -5.0, 20.0)
confidence = Log10(ClosedTrades + 1)
Score      = pf * NetExpectancy * confidence * (1.0 + rtd * 0.1)
```

Validação OOS: `ClosedTrades >= 15` E `NetPnL > 0`

---

## Mapeamento strategy → regime

| Strategy | Regimes permitidos |
|---|---|
| Momentum | Trend |
| Ema | Trend |
| Trend | Trend |
| SchoolRun | Trend |
| OrbBreakout | Breakout |
| IbBreakout | Breakout + Trend |
| Range | Range |
| VwapReversion | Range |
| BollingerFade | Range |
| Volatility | HighVolatility |

`NonTrend` → nunca permitido em nenhuma strategy.  
`Undefined` → incluído por padrão para não reduzir demais o dataset em strategies sem regime claro.

---

*Última atualização: 2026-05-16*  
*Testes passando: 68*  
*Dataset: mnq_5m_12mo.csv (82.659 barras, mar/2025–mai/2026)*
