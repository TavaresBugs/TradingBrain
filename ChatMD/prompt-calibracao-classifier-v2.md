# Prompt — Calibração do RegimeClassifier: Diagnosticar e Ajustar Thresholds

Leia `ChatMD/CLAUDE.md` antes de começar. Depois:

```bash
cat src/TradingBrain.Core/RegimeClassifier.cs
```

---

## Contexto

O classifier atual tem thresholds hardcoded sem base empírica:

```csharp
// Thresholds atuais — todos hardcoded, nenhum tem justificativa documentada
NonTrend:    ibFullToday < 0.05
HighVol:     overnightRatio > 2.0
Breakout:    openOutside && (overnightRatio > 1.0 || gapRatio > 0.40)
Trend:       openOutside && overnightRatio <= 1.0 && gapRatio <= 0.40 && ibFull <= 0.75
Range:       !openOutside && cperiodInside
Undefined:   tudo mais
```

Resultado empírico em 285 dias:
- Undefined: 57 dias (20%) — inaceitável, classifier não sabe o que são esses dias
- HighVolatility: 0 dias — threshold `> 2.0` nunca é atingido no MNQ

**O objetivo não é forçar uma distribuição bonita.** É entender por que esses dias
estão sem classificação e corrigir só o que o dado justificar.

---

## PASSO 1 — Extrair dados brutos do regime report

```bash
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --regime-report outputs/tv-bars/mnq_5m_12mo.csv outputs/calibration-diag \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

Abra `outputs/calibration-diag/regime_report.html` e colete **três números**:

```
1. Breakout.OvernightAvg  → ___   (referência para calibrar HighVol)
2. Breakout.GapAvg        → ___   (referência para o threshold de gap)
3. Undefined clusters:
   - Cluster com mais dias → nome: ___, dias: ___, OvernightAvg: ___, IbFullAvg: ___
   - Segundo cluster       → nome: ___, dias: ___, OvernightAvg: ___, IbFullAvg: ___
```

---

## PASSO 2 — Diagnosticar cada problema separadamente

### Problema A: HighVolatility com 0 dias

`overnightRatio > 2.0` significa que o overnight foi 2x o ATR14 diário.
No MNQ, com ATR14 ~20pts, isso requer overnight de 40pts. Isso quase nunca ocorre.

**Diagnóstico:** o threshold `2.0` foi inventado. O valor correto é empírico.

Regra de calibração:
```
HighVol threshold = Breakout.OvernightAvg × 2.0
```
Se `Breakout.OvernightAvg = 0.6` → threshold HighVol = `1.2` (não `2.0`).
Se `Breakout.OvernightAvg = 0.4` → threshold HighVol = `0.8`.

Aplique e veja quantos dias viram HighVolatility. Se forem 0–5 dias → threshold ainda
alto demais, reduza para `Breakout.OvernightAvg × 1.5`. Se forem > 30 dias → alto demais
(está absorvendo Breakout normal), aumente para `× 2.5`.

**O alvo não é uma cota.** É: "dias com overnight excepcional que não são apenas Breakout normal".

---

### Problema B: Undefined com 57 dias

O cluster report da seção 3 agrupa os Undefined por padrão. Use esses clusters para decidir:

| Cluster | O que provavelmente é | Ação |
|---|---|---|
| `NoStrongSignal` | openInside, overnight moderado, IB normal | → Range: o `cperiodInside` está bloqueando |
| `OpenOutside+NarrowIB` | openFora mas IB estreito | → Trend: ibFull ≤ 0.75, mas não passou em algo |
| `OpenOutside+NormalIB` | openFora, IB normal | → Breakout: está caindo fora do threshold |
| `HighGap+NoBreakout` | gap grande mas overnight baixo | → Breakout: threshold de gap pode estar alto |
| `WideIB` | IB muito largo | → Breakout ou deixar Undefined |
| `HighOvernight+NoBreakout` | overnight entre 1.0 e 2.0 | → HighVol depois da calibração do threshold |

**Regra:** só absorva um cluster em outro regime se o dado justificar.
Não absorva `WideIB` em Range só para reduzir Undefined — são dias anômalos.

---

### Problema C: Range com `cperiodInside` como único gatilho

`cperiodInside` exige que TODAS as barras de 10:30–10:55 fiquem dentro do IB de hoje.
Isso é muito restritivo e captura apenas dias de range perfeito.

Dias `NoStrongSignal` (openInside, overnight quieto, sem direcionalidade) são Range
por definição de mercado — o fato do C-period ter tocado marginalmente fora é ruído.

**Substitua a condição de Range:**
```csharp
// ANTES — muito restritivo:
Range: !openOutside && cperiodInside

// DEPOIS — open dentro + overnight quieto = ausência de força direcional:
Range: !openOutside && overnightRatio <= RangeOvernightThreshold
```

O valor de `RangeOvernightThreshold` vem do dado:
```
Se Undefined.NoStrongSignal.OvernightAvg = X → RangeOvernightThreshold = X × 1.5
```

`cperiodInside` continua sendo calculado e armazenado no `DayRegime` — não remova o campo.
Apenas deixa de ser critério de classificação.

---

## PASSO 3 — Extrair thresholds e adicionar como constantes nomeadas

**Esta é a mudança mais importante estruturalmente.**

Em `RegimeClassifier.cs`, antes da função `Classify()`, adicione:

```csharp
// Thresholds — calibrados empiricamente em MNQ 14 meses (2024-2025)
// Rever se dataset mudar significativamente ou instrumento trocar
private const double NonTrendIbThreshold      = 0.05;   // ibFull < X → dia sem range
private const double HighVolOvernightThreshold = [CALCULADO_PASSO_2A]; // overnight >> normal
private const double BreakoutOvernightThreshold = 1.0;  // overnight > X → força direcional
private const double BreakoutGapThreshold       = 0.40; // gap > X → Breakout
private const double TrendIbFullMaxThreshold    = 0.75; // ibFull <= X → Trend (não Breakout)
private const double RangeOvernightThreshold    = [CALCULADO_PASSO_2C]; // overnight <= X → Range
```

Substitua todos os literais na função `Classify()` por essas constantes.
Isso permite que futuras calibrações sejam feitas em um lugar só, sem caçar números espalhados.

---

## PASSO 4 — Validar com backtest, não só com distribuição

Após aplicar os thresholds calibrados:

```bash
# Distribuição — sanity check
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --regime-report outputs/tv-bars/mnq_5m_12mo.csv outputs/calibration-v2 \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# Backtest — validação real
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --backtest-regime outputs/tv-bars/mnq_5m_12mo.csv outputs/backtest-calibrado \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

**Critério de aceite da calibração:**
- Undefined deve cair — idealmente para < 15 dias
- HighVolatility deve ter pelo menos alguns dias — se ainda for 0 após calibração, o dataset
  pode não ter períodos de alta volatilidade (possível com 14 meses de dado)
- **O PF das strategies candidatas (Trend, Momentum) não pode cair significativamente**
  — se cair, a calibração mudou a composição dos dias de forma prejudicial e deve ser revertida
- Distribuição razoável como sanity check: Range não pode ser < 15% nem > 60%

---

## Reporte ao concluir

```
[Passo 1 — Dados brutos]
Breakout.OvernightAvg:  ___
Breakout.GapAvg:        ___
Undefined top clusters:
  1. ___ → ___ dias, overnight ___, ibFull ___
  2. ___ → ___ dias, overnight ___, ibFull ___

[Passo 3 — Constantes adicionadas]
NonTrendIbThreshold:        ___  (era 0.05)
HighVolOvernightThreshold:  ___  (era 2.0)
RangeOvernightThreshold:    ___  (novo)

[Passo 4 — Distribuição após calibração]
Trend:          ___ dias
Breakout:       ___ dias
Range:          ___ dias
HighVolatility: ___ dias
NonTrend:       ___ dias
Undefined:      ___ dias  ← deve cair de 57

[Passo 4 — Backtest após calibração]
Strategy    | PF antes | PF depois | Aceito?
Trend       | 1.85     |           |
Momentum    | 1.25     |           |
IbBreakout  | 1.09     |           |
VwapRev     | 0.91     |           |
```
