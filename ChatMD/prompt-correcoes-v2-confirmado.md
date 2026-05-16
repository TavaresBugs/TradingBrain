# Prompt — Correções Confirmadas: EMA + IbBreakout + SchoolRun + Program.cs

Leia `ChatMD/CLAUDE.md` antes de começar. Depois leia os arquivos abaixo antes de qualquer edição:

```bash
cat src/TradingBrain.Console/StrategyRules.cs
cat src/TradingBrain.Core/BacktestModels.cs
cat src/TradingBrain.Core/StrategyRegimeMap.cs
cat src/TradingBrain.Console/Program.cs
```

---

## Contexto empírico

Rodamos grid search e walk-forward no dataset real (82.659 barras, 14 meses).
Resultado confirmado:

| Strategy | Trades sem filtro | Trades com filtro | Problema |
|---|---:|---:|---|
| Ema | ? | 0 | Bug: `bar.Close > SwingHigh` sempre falso |
| IbBreakout | 3 | 0 | Bug: lógica de entrada não dispara |
| SchoolRun | 197 | 26 | Regime muito restrito: Trend only → WF cai 2/5 → 0/5 |

---

## CORREÇÃO 1 — EMA: bug estrutural no SwingHigh (CERTEZA)

### Causa

Em `StrategyBacktester.Run()`:
```csharp
highs.Add(bar.High);   // ← bar atual adicionado ANTES de BuildMetrics
var metrics = BuildMetrics(..., highs, ...);
```

Em `BuildMetrics`:
```csharp
["SwingHigh"] = highs.Count < 20 ? double.NaN : highs.TakeLast(20).Max(),
["SwingLow"]  = lows.Count  < 20 ? double.NaN : lows.TakeLast(20).Min(),
```

Como `highs` já contém `bar.High` do bar atual, e `bar.High >= bar.Close` sempre:
```
SwingHigh >= bar.High >= bar.Close
→ bar.Close > SwingHigh  →  SEMPRE FALSO
```

EMA nunca entra. Zero trades é o comportamento matematicamente correto dado o bug.

### Fix

Em `BuildMetrics` dentro de `StrategyRules.cs`, substitua:

```csharp
// ANTES:
["SwingHigh"] = highs.Count < 20 ? double.NaN : highs.TakeLast(20).Max(),
["SwingLow"]  = lows.Count  < 20 ? double.NaN : lows.TakeLast(20).Min(),

// DEPOIS — exclui o bar atual, usa os 20 bars anteriores:
["SwingHigh"] = highs.Count < 21 ? double.NaN : highs.SkipLast(1).TakeLast(20).Max(),
["SwingLow"]  = lows.Count  < 21 ? double.NaN : lows.SkipLast(1).TakeLast(20).Min(),
```

**Isso afeta TODAS as strategies que usam SwingHigh/SwingLow.** Verifique se alguma outra strategy usa esses valores — se sim, confirme que o comportamento novo é correto para elas também.

---

## CORREÇÃO 2 — IbBreakout: diagnosticar e corrigir a lógica de entrada

### O que sabemos

3 trades em 82.659 barras (14 meses) sem nenhum filtro de regime.
A implementação existe (68/68 testes passando), mas raramente dispara.

### O que fazer

1. Leia `EvaluateIbBreakout` em `StrategyRules.cs`
2. Identifique como o IB range é calculado e qual a condição de entrada

**Se a condição de entrada for `bar.Close > ibHigh`:** troque por:
```csharp
// ANTES:
if (bar.Close > ibHigh)
    return new StrategyDecision(SignalAction.Buy, "IB breakout long");
if (bar.Close < ibLow)
    return new StrategyDecision(SignalAction.Sell, "IB breakout short");

// DEPOIS — trigger intrabar, confirmação pelo fechamento acima do midpoint:
var ibMid = (ibHigh + ibLow) / 2.0;
if (bar.High > ibHigh && bar.Close > ibMid)
    return new StrategyDecision(SignalAction.Buy, "IB breakout long");
if (bar.Low < ibLow && bar.Close < ibMid)
    return new StrategyDecision(SignalAction.Sell, "IB breakout short");
```

**Se `IbMinRangeRatio` tiver default > 0.1:** reduza para `0.0` em `BacktestModels.cs`.
O grid search vai explorar esse parâmetro — o default não deve pré-filtrar dias.

**Se o IB for calculado com `bar.Close` em vez de `bar.High`/`bar.Low`:** corrija para:
```csharp
var ibHigh = ibBars.Max(b => b.High);
var ibLow  = ibBars.Min(b => b.Low);
```

Após o fix, o smoke test deve gerar **pelo menos 30 trades** no dataset completo sem filtro.

---

## CORREÇÃO 3 — SchoolRun: adicionar Breakout ao regime

### Evidência

- Sem filtro: 197 trades IS, WF = 2/5 janelas positivas
- Com filtro Trend: 26 trades IS, WF = 0/5 janelas positivas

O filtro Trend deixa apenas ~28 dias IS — insuficiente para o walk-forward.
SchoolRun opera em qualquer dia com direcionalidade de abertura, não só em dias Trend puro.

### Fix

Em `StrategyRegimeMap.cs`, localize a linha de SchoolRun e adicione Breakout:

```csharp
// ANTES:
Add(map, "SchoolRun", MarketRegime.Trend);

// DEPOIS:
Add(map, "SchoolRun", MarketRegime.Trend, MarketRegime.Breakout);
```

Trend (47 dias) + Breakout (37 dias) = 84 dias totais no dataset.
Range e NonTrend continuam excluídos — nesses dias não há direcionalidade de abertura.

Atualize a tabela em `ChatMD/CLAUDE.md`:
```
| SchoolRun | Trend + Breakout |
```

---

## CORREÇÃO 4 — Program.cs: GridSearchStrategies desatualizada

### Evidência

`GridSearchStrategies()` usa lista hardcoded que ainda referencia `GoldBreakout` (removido do enum) e não inclui as 6 strategies novas.

### Fix

Localize em `Program.cs`:
```csharp
static IReadOnlyList<StrategyKind> GridSearchStrategies(StrategyKind? requestedStrategy)
```

Substitua o fallback:
```csharp
// ANTES (lista velha com GoldBreakout):
return new[] { StrategyKind.Momentum, StrategyKind.GoldBreakout, StrategyKind.Ema, StrategyKind.Range };

// DEPOIS (todas as 10 strategies atuais):
return new[]
{
    StrategyKind.Momentum,
    StrategyKind.Ema,
    StrategyKind.Trend,
    StrategyKind.SchoolRun,
    StrategyKind.OrbBreakout,
    StrategyKind.IbBreakout,
    StrategyKind.Range,
    StrategyKind.VwapReversion,
    StrategyKind.BollingerFade,
    StrategyKind.Volatility,
};
```

---

## Verificação

```bash
# 1. Build limpo
dotnet build ./TradingBrain.slnx

# 2. Todos os testes passando
dotnet test ./TradingBrain.slnx --verbosity normal
# Esperado: 68/68 (mínimo)

# 3. Smoke EMA — deve gerar trades agora
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  outputs/tv-bars/mnq_5m_12mo.csv outputs/smoke-ema --strategy Ema \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
# Esperado: ClosedTrades > 0

# 4. Smoke IbBreakout — deve gerar muito mais que 3 trades
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  outputs/tv-bars/mnq_5m_12mo.csv outputs/smoke-ib --strategy IbBreakout \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
# Esperado: ClosedTrades >= 30

# 5. Smoke SchoolRun — confirmar que o regime map mudou
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/smoke-schoolrun SchoolRun \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
# Esperado: log mostra "X/Y dias úteis (Trend+Breakout)" com X bem maior que 28
```

---

## O que NÃO alterar

- **Volatility:** 2 trades em 9 dias HV = problema de dados, não de código. Não tocar.
- **BollingerFade:** NetPF IS 0.91 é questão de tuning, não bug estrutural. Não tocar agora.
- **Testes existentes:** 68/68 deve continuar passando. Se o fix do SwingHigh quebrar algum teste de EMA ou Ema, ajuste o teste para refletir o comportamento correto novo.

---

## Reporte ao concluir

```
dotnet test: ___/68 passando
Smoke EMA ClosedTrades: ___
Smoke IbBreakout ClosedTrades: ___
SchoolRun dias filtrados após fix: ___/237
```
