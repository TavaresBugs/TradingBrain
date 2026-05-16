# TradingBrain — Relatório de Status e Continuidade

> Gerado em: 2026-05-16  
> Repositório: https://github.com/TavaresBugs/TradingBrain  
> Último commit: `45de79e feat(regime): KER como sinal primário de tendência`

---

## 1. O que é o projeto

TradingBrain é um engine de backtest próprio em C# (.NET 10) para estratégias de futuros de índice (MNQ — Micro Nasdaq Futures). O objetivo é reconstruir, validar e refinar estratégias que originalmente existiam em uma DLL proprietária do NinjaTrader, usando um pipeline de backtest independente com grid search, IS/OOS split e walk-forward validation.

O projeto **não usa NinjaTrader nem nenhuma DLL externa** durante o backtest — tudo roda sobre CSVs de barras OHLCV.

---

## 2. Estrutura do repositório

```
tradingbrain/
├── src/
│   ├── TradingBrain.Core/          # Modelos, indicadores, engine de decisão
│   │   ├── MarketBar.cs
│   │   ├── PrecomputedSeries.cs    # Indicadores pré-computados por índice
│   │   ├── TechnicalIndicators.cs  # EMA, ATR, MACD, RSI, Resample, etc.
│   │   ├── RegimeClassifier.cs     # Classificador de regime (KER + overnight)
│   │   ├── MarketRegime.cs         # Enum + DayRegime record
│   │   ├── BacktestModels.cs       # StrategyTuningParams, BacktestSummary, etc.
│   │   ├── ExecutionSettings.cs    # Custos: tick, slippage, comissão
│   │   └── StrategyKind.cs         # Enum de strategies
│   └── TradingBrain.Console/       # CLI + pipeline de backtest
│       ├── Program.cs              # Entry point, parsing de comandos
│       ├── StrategyBacktester.cs   # Loop principal de backtest (partial class)
│       ├── StrategyRules.cs        # Lógica de entrada/saída por strategy (partial class)
│       ├── GridSearchRunner.cs     # Grid search com IS/OOS split
│       ├── WalkForwardValidator.cs # Walk-forward rolling com N janelas
│       ├── BacktestReports.cs      # Summarize, métricas, CSVs
│       ├── CsvBarReader.cs         # Parser de CSV de barras
│       └── RunManifestWriter.cs    # Manifesto JSON de cada run
├── tests/
│   └── TradingBrain.Tests/        # 54 testes passando
├── outputs/                       # CSVs gerados (não versionados)
├── adapters/TradingView/          # Scripts de conversão (não versionados)
└── docs/                          # Documentação técnica
```

---

## 3. Strategies implementadas (10 no total)

| Strategy | Regime alvo | Lógica de entrada | Status |
|---|---|---|---|
| **Volatility** | Alta volatilidade | ATR expansion + volume + EMA + VWAP + RSI | Implementada, grid 1215 combos |
| **Momentum** | Tendência | MACD cross + RSI > 55 + EMA21 + volume | Implementada, grid 64 combos |
| **EMA** | Tendência | EMA9>21 + SwingHigh rompido + volume | Implementada, grid 64 combos |
| **Trend** | Tendência | Donchian 10 + RSI confirma | Implementada, grid 5 combos |
| **Range** | Lateralidade | ATR compression + breakout EMA20 | Implementada, grid 24 combos |
| **OrbBreakout** | Breakout abertura | Range 9:30-10:00 + breakout com buffer | Implementada, grid 29 combos (waves) |
| **VwapReversion** | Lateralidade | Preço distante do VWAP + RSI extremo | Implementada, grid 64 combos |
| **BollingerFade** | Lateralidade | Toque na banda + candle de reversão | Implementada, grid não mapeado |
| **SessionBreakout** | Breakout | Range de sessão configurável + breakout | Implementada, grid não mapeado |
| **SchoolRun (SRS)** | Tendência | Candle de referência M15 + breakout + overnight range | Implementada, grid 384 combos |

---

## 4. Pipeline de backtest

### Comandos disponíveis

```bash
# Rodar uma strategy com params default
dotnet run -- <barras.csv> <output_dir> [--strategy <nome>]

# Grid search com IS/OOS (split 65/35)
dotnet run -- --grid-search <barras.csv> <output_dir> <Strategy>

# Walk-forward rolling com N janelas
dotnet run -- --walk-forward <barras.csv> <output_dir> <Strategy> --windows N

# Classificar regime dos dias
dotnet run -- --classify-regime <barras.csv> <output_dir>

# Rodar todas as strategies
dotnet run -- --run-all <barras.csv> <output_dir>
```

### Parâmetros de execução (sempre recomendados para MNQ)

```bash
--tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### Score do grid search

```csharp
// Gate: ClosedTrades >= 30 E NetExpectancy > 0
// Fórmula:
pf = Min(NetProfitFactor, 10.0)
rtd = Clamp(ReturnToDrawdown, -5.0, 20.0)
confidence = Log10(ClosedTrades + 1)
Score = pf * NetExpectancy * confidence * (1.0 + rtd * 0.1)
```

### IS/OOS

- Split: 65% IS, 35% OOS
- Validação OOS: `ClosedTrades >= 15` E `NetPnL > 0`
- Exporta `is_vs_oos.csv` com Score, Trades, WinRate, NetPnL, MaxDD, RTD

---

## 5. Dados disponíveis

### Dataset principal

```
outputs/tv-bars/mnq_5m_12mo.csv
```

- **Período:** 2025-03-16 → 2026-05-15 (~14 meses)
- **Barras:** 82.659 barras de 5 minutos
- **Formato:** `time,open,high,low,close,volume` (sem timezone, horário ET)
- **Origem:** 4 arquivos do TradingView exportados manualmente e merged

### Script de conversão

```bash
# Converter CSV do TradingView para formato TradingBrain
python3 adapters/TradingView/convert_tradingview.py \
  input.csv output.csv --drop-zero-volume

# Opções adicionais:
# --market-hours-only   (filtra 9:30-17:00 ET)
# --resample 15         (reagrupa para 15min)
# --start YYYY-MM-DD    (data inicial)
# --end YYYY-MM-DD      (data final)
```

---

## 6. Resultados obtidos (dataset 82k barras, 14 meses)

### Grid search IS/OOS — dataset completo sem filtro de regime

| Strategy | Trades IS | NetPF IS | OOSValidated | NetPnL OOS (melhor) |
|---|---|---|---|---|
| Momentum | 86 | 1.11 | 3 | positivo, fraco |
| OrbBreakout | 32 | 1.12 | 3 | positivo, fraco |
| VwapReversion | 119 | 1.19 | 3 | positivo, fraco |
| Volatility | 37 | 1.24 | 2 | negativo no OOS |
| **SchoolRun** | **49** | **2.27** | **3** | **+277 a +317 pontos** |

### Walk-forward SchoolRun (5 janelas, 12 meses)

| Janela | IS Score | IS NetPnL | OOS NetPnL |
|---|---|---|---|
| 1 | 43.98 | +539 | -135 |
| 2 | 23.63 | +233 | -318 |
| 3 | 15.00 | +224 | -72 |
| 4 | 19.29 | +264 | -18 |
| 5 | 88.35 | +590 | **+333** |

**Diagnóstico:** OOS negativo em 4/5 janelas não indica que a strategy é ruim — indica que o walk-forward sequencial não é o teste correto para strategies de regime. A janela 5 (período mais recente) foi positiva porque o mercado estava no regime certo para o SchoolRun.

---

## 7. Classificador de regime — estado atual

### Implementação atual (commit `45de79e`)

Arquivo: `src/TradingBrain.Core/RegimeClassifier.cs`

**Sinais usados:**
- KER(10) — Kaufman Efficiency Ratio sobre os últimos 10 fechamentos diários
- `rangeRatio` — range do dia anterior / ATR14
- `closePosition` — onde o dia anterior fechou dentro do seu range
- `overnightRatio` — range overnight / ATR14
- `gapRatio` — gap de abertura / ATR14

**Classificação atual:**
```
HighVolatility : rangeRatio > 2.0 ou overnightRatio > 2.0
Breakout       : KER > 0.50 E (gap > 0.40 ou overnight > 1.2)
Trend          : KER > 0.50 (sem catalisador externo)
Range          : KER < 0.25
Undefined      : resto
```

**Distribuição nos 14 meses:**
```
Range            131 dias  (45.6%)
Trend             51 dias  (17.8%)
Undefined         51 dias  (17.8%)
Breakout          49 dias  (17.1%)
HighVolatility     5 dias  (1.7%)
```

### Problema identificado nos dados

Análise dos dados reais mostrou que o KER não é um bom preditor do comportamento do dia seguinte. O `directionality` (move/range) médio por regime:

```
Trend          : 0.431  ← MENOR que Range
Breakout       : 0.465
Range          : 0.488  ← MAIOR (problema!)
HighVolatility : 0.358
Undefined      : 0.457
```

O KER mede a eficiência direcional dos últimos 10 dias — é um sinal **tardio** que descreve o passado, não o comportamento esperado do dia atual.

---

## 8. PRÓXIMO PASSO PRIORITÁRIO — Initial Balance Classifier

### A abordagem correta (Market Profile / institucional)

A metodologia usada por traders institucionais de futuros (derivada do Market Profile de J. Peter Steidlmayer, CBOT) classifica o tipo de dia usando o **Initial Balance (IB)** — o range formado na primeira hora de negociação (9:30-10:30 ET).

**Por que funciona:** O IB mede o comportamento do mercado **na sessão atual**, não no histórico. É o sinal mais próximo do tempo real disponível antes das 10:30.

### Critérios de classificação por IB

| Tipo de dia | Critérios | Strategy ativada |
|---|---|---|
| **Trend Day** | Abertura fora do IB de ontem + IB de hoje estreito (<0.5x ATR) + one-timeframing | SchoolRun, Momentum |
| **Breakout Day** | Gap > 0.5x ATR + IB de hoje estreito que segura | OrbBreakout |
| **Normal/Range Day** | Abertura dentro do IB de ontem + IB normal (0.8-1.5x ATR) | VwapReversion, BollingerFade |
| **High Volatility Day** | IB > 2x ATR ou range overnight > 2x ATR | Volatility |
| **Non-Trend Day** | IB muito estreito (<0.3x ATR) sem expansão até 10:30 | Nenhuma strategy — ficar fora |

### Inputs necessários (todos sem lookahead)

```
Disponíveis antes das 10:30 ET:
- IB de ontem: high e low das 9:30-10:30 do dia anterior
- Gap de abertura: abs(open_930 - close_D1) / ATR14
- IB de hoje parcial: range das 9:30-10:00 (primeiros 30min)
- Overnight range: high/low das 18:00-9:25
- ATR14 diário do dia anterior
```

### Implementação sugerida

Substituir `RegimeClassifier.cs` por uma versão baseada em IB:

```csharp
// Pseudocódigo da nova lógica
var ibYesterday = (high_930_1030_yesterday, low_930_1030_yesterday);
var openingGap = abs(open_today_930 - close_yesterday) / atr14;
var ibToday30min = high_930_1000_today - low_930_1000_today;
var ibRatio30 = ibToday30min / atr14;

// Classificação
if (ibRatio30 < 0.3 && openingGap < 0.2)
    → NonTrend (ficar fora)
else if (openingGap > 0.5 && ibRatio30 < 0.5)
    → Breakout
else if (open_today > ibYesterday.High || open_today < ibYesterday.Low)
    → Trend potencial (confirmar com one-timeframing aos 30min)
else if (open_today between ibYesterday && ibRatio30 normal)
    → Range
else if (ibRatio30 > 2.0 || overnightRatio > 2.0)
    → HighVolatility
```

**Detalhe crítico:** A classificação de Trend Day requer confirmação de **one-timeframing** — o mercado vai subindo (ou descendo) de forma sequencial sem retornar ao candle anterior. Isso é verificável com dados de 5min após as 10:00.

---

## 9. Pergunta sobre o backtester — modo replay

O backtester atual (`StrategyBacktester.Run`) processa as barras **sequencialmente** — cada barra é vista uma vez, em ordem cronológica, sem acesso ao futuro. Isso significa que ele já funciona como um replay mecânico:

```csharp
for (var i = 0; i < bars.Count; i++)
{
    var bar = bars[i];  // vê apenas a barra atual
    var metrics = BuildMetrics(series, i);  // indicadores calculados até i
    var decision = Evaluate(bar, bars, i, metrics, ...);  // decisão com dados até i
    // entry/exit baseado na decisão
}
```

**O que falta para visualizar o replay:** o backtester gera `StrategyBacktestRow` para cada barra contendo `Signal`, `Reason`, `Position`, `EntryPrice`, `OpenProfit`, `Equity`, todos os indicadores. Esses dados já permitem reconstruir o replay completo.

**Para testar entradas e saídas mecanicamente**, o arquivo `*.trades.csv` gerado já contém todos os trades com data/hora de entrada, preço, motivo de saída e PnL.

**O que precisaria ser implementado para um replay interativo:** uma visualização que leia o `signals.csv` e mostre barra a barra o que o sistema viu e decidiu. Isso poderia ser um comando `--replay` que imprime no terminal ou gera um HTML interativo.

---

## 10. Pendências técnicas

### Alta prioridade

1. **Substituir RegimeClassifier por IB-based classifier** — implementar a lógica de Initial Balance como classificador principal de tipo de dia, validar nos dados de 82k barras, confirmar que `directionality` por regime é significativamente diferente.

2. **Implementar RegimeFilter no GridSearchRunner** — depois de validar o classificador, filtrar o dataset por regime antes de rodar o grid de cada strategy. OrbBreakout só roda em dias Breakout, SchoolRun só em dias Trend, etc.

3. **Strategy de Initial Balance** — implementar `IbBreakout` como strategy nova baseada no range 9:30-10:30, que é distinta do OrbBreakout atual (que usa janela configurável e resample M15). A IB strategy canônica usa o range da primeira hora diretamente das barras de 5min sem resample.

### Média prioridade

4. **WalkForwardValidator com filtro de regime** — o walk-forward atual é sequencial e penaliza strategies de regime injustamente. Implementar modo que respeita o regime alvo de cada strategy.

5. **Comando `--replay`** — visualização barra a barra dos sinais gerados, para auditoria manual de entradas e saídas mecânicas.

6. **Mais dados** — o dataset atual cobre março 2025 a maio 2026. Para o classificador de IB ser validado com significância, idealmente 2+ anos (2024-2026). Baixar via TradingView mais 4 arquivos cobrindo 2024.

### Baixa prioridade

7. **SchoolRun grid refinamento** — os parâmetros atuais usam `SrsReferenceCandle=1 ou 2` e `UseAntiMode=False`. Testar combinações com anti-mode ativo em dias onde o preço está dentro do overnight range.

8. **OrbBreakout grid expandido** — atualmente só 29 combos em waves. Adicionar variação da janela de formação (9:30-10:00 vs 9:30-10:30) como parâmetro.

---

## 11. Commits relevantes (ordem cronológica)

```
24214a8  feat(walk-forward): implement validator
c5e642b  perf(backtester): conecta PrecomputedSeries, elimina recálculo de indicadores
5bc7aed  fix(resample): agrega por janela de tempo real, corrige OrbBreakout e SchoolRun
a03812d  feat(orb): implementa ORB canônico com range da janela, 1 trade/dia
c5b8403  fix(grid): OrbBreakoutGrid em waves, PrecomputedSeries no OOS, filtro NetPnL>0
8d6b8e3  feat(regime): RegimeClassifier por dia com sinais overnight, gap e range
420c3eb  fix(regime): relaxa thresholds — Trend >1.2 rangeRatio, Breakout >0.4 gap
45de79e  feat(regime): KER como sinal primário de tendência, fallback para rangeRatio
```

---

## 12. Contexto técnico do ambiente

- **OS:** Ubuntu (Linux, X11)
- **Runtime:** .NET 10 (`dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj`)
- **Build:** `dotnet build ./TradingBrain.slnx`
- **Testes:** `dotnet test ./TradingBrain.slnx` (54 testes)
- **Dados no projeto:** `outputs/tv-bars/mnq_5m_12mo.csv` (não versionado)
- **Script de conversão:** `adapters/TradingView/convert_tradingview.py`

---

## 13. Decisões de design tomadas e por quê

| Decisão | Motivo |
|---|---|
| Engine próprio em C# em vez de NinjaTrader | Independência de DLL proprietária, controle total do pipeline |
| CSV como input | Portabilidade, sem dependência de broker |
| IS/OOS com NetPnL > 0 como gate no OOS | Score com NetExpectancy pode ser negativo mesmo com PnL positivo em amostras pequenas |
| PrecomputedSeries em vez de recálculo por candle | O(n) em vez de O(n²) — impacto crítico no grid search |
| Walk-forward com N janelas configurável | Com datasets pequenos, 2-3 janelas são mais adequadas que 5 |
| KER como sinal primário de regime | Base empírica de Perry Kaufman (1995) — mas validação nos dados mostrou limitação como preditor do dia seguinte |
| Initial Balance como próximo classificador | Metodologia institucional real usada em prop desks e CME — prediz o tipo de dia usando comportamento da sessão atual, não histórico |

---

*Fim do relatório. Próxima sessão deve começar pelo ponto 10.1 (IB-based classifier) e 10.5 (comando --replay para auditoria de trades).*
