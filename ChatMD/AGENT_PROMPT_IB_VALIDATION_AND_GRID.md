# Prompt — Validação IB Classifier + Grid Search IbBreakout

Leia `ChatMD/CLAUDE.md` antes de começar. Depois leia os arquivos abaixo antes de qualquer edição:

```bash
cat src/TradingBrain.Core/RegimeClassifier.cs
cat src/TradingBrain.Core/MarketRegime.cs
cat src/TradingBrain.Console/StrategyRules.cs       # apenas EvaluateIbBreakout
cat src/TradingBrain.Core/BacktestModels.cs         # apenas IbBreakout params
cat src/TradingBrain.Console/BacktestReports.cs
```

---

## Contexto

Commit atual: `2160b89 fix: restore EMA and IB breakout signals`  
Testes: 68/68 passando  
Smoke pós-fix confirmado:
- EMA: 1.094 trades
- IbBreakout: 279 trades
- SchoolRun: 50/237 dias filtrados (Trend+Breakout), 3 janelas OOS validadas

O IB classifier substituiu o KER-based em commit anterior. O problema identificado com o KER era que dias classificados como "Range" tinham `directionality` (move/range) maior do que dias "Trend" — o inverso do esperado. O IB classifier não foi validado empiricamente nos dados reais ainda. Esta sessão valida isso e, confirmando o classificador, roda o grid do IbBreakout com filtro de regime.

---

## TAREFA 1 — Validar o IB classifier empiricamente

### O que fazer

Rodar o `--classify-regime` no dataset principal e calcular, para cada regime, a média do `directionality` dos dias classificados.

**Definição de directionality:**
```
directionality = abs(close_dia - open_dia) / (high_dia - low_dia)
```
onde `open_dia`, `high_dia`, `low_dia`, `close_dia` são os valores da sessão regular (9:30-16:00 ET) do dia.

### Critério de aprovação do classifier

| Regime | Directionality esperada |
|---|---|
| Trend | > 0.50 |
| Breakout | > 0.45 |
| Range | < 0.40 |
| HighVolatility | qualquer valor (poucos dias) |
| NonTrend | < 0.30 |

Se Trend tiver directionality **menor** que Range, o classifier está classificando errado — revisar os thresholds em `RegimeClassifier.cs`.

### Como executar

```bash
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --classify-regime outputs/tv-bars/mnq_5m_12mo.csv outputs/regime-validation \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

O output deve gerar um CSV com a classificação de cada dia. Se o CSV não tiver o campo `directionality`, calcule em um script Python simples ou adicione ao `BacktestReports` o cálculo agregado por regime.

### Se o classifier precisar de ajuste

Leia `RegimeClassifier.cs` e ajuste apenas os thresholds do IB. Não mude a lógica estrutural — apenas os valores numéricos. Após ajuste:

1. `dotnet build ./TradingBrain.slnx` — deve passar
2. `dotnet test ./TradingBrain.slnx --verbosity normal` — 68/68 mínimo
3. Re-rodar o classify-regime e confirmar que os critérios de aprovação são atendidos

---

## TAREFA 2 — Grid Search IbBreakout com filtro de regime

### Pré-condição

Executar esta tarefa **somente após a Tarefa 1 ser aprovada**. Se o classifier estiver errado, o grid filtrado será inválido.

### Execução

```bash
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-ibbreakout IbBreakout \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

### Critérios de sucesso

```
[RegimeFilter] IbBreakout: X/Y dias úteis (Breakout+Trend)
```
Esperado: X >= 40 dias após filtro (Breakout ~37 + Trend ~47 = ~84 dias no dataset)

```
Grid search concluído. OOSValidated >= 3
```
Combinações OOS válidas: pelo menos 3 combinações com `ClosedTrades >= 15` E `NetPnL > 0`

### Se OOSValidated = 0

Diagnóstico em ordem:
1. Verificar se o regime filter está retornando dias suficientes (log `[RegimeFilter]`)
2. Verificar se `IbMinRangeRatio` default está acima de 0 — se sim, reduzir para `0.0` em `BacktestModels.cs`
3. Verificar se o IB (9:30-10:30) está sendo calculado corretamente — `ibHigh = Max(b.High)`, `ibLow = Min(b.Low)` das barras de 5min nessa janela
4. Adicionar log temporário em `EvaluateIbBreakout` contando quantas vezes a condição de entrada é avaliada vs. quantas vezes dispara

---

## TAREFA 3 — Walk-forward IbBreakout (se grid aprovado)

Executar somente se a Tarefa 2 tiver `OOSValidated >= 3`.

```bash
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --walk-forward outputs/tv-bars/mnq_5m_12mo.csv outputs/wf-ibbreakout IbBreakout --windows 3 \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

Usar 3 janelas (não 5) — com ~84 dias filtrados, 5 janelas deixa IS muito pequeno.

Critério de aprovação: `>= 1 janela OOS positiva`

---

## TAREFA 4 — Grid Search comparativo SchoolRun (bônus)

Comparar SchoolRun antes e depois do fix do regime map (Trend → Trend+Breakout).

```bash
# Com filtro Trend+Breakout (default atual)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-schoolrun-filtered SchoolRun \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# Sem filtro de regime (baseline de comparação)
dotnet run --project src/TradingBrain.Console/TradingBrain.Console.csproj -- \
  --grid-search outputs/tv-bars/mnq_5m_12mo.csv outputs/grid-schoolrun-nofilter SchoolRun \
  --no-regime-filter \
  --tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62
```

Comparar `is_vs_oos.csv` dos dois outputs:
- Quantas combinações OOS validadas em cada caso
- Qual tem melhor Score médio no top-5

---

## Verificação final

Ao concluir, reporte:

```
dotnet test: ___/68 passando

Tarefa 1 — IB Classifier:
  Trend directionality: ___
  Breakout directionality: ___
  Range directionality: ___
  NonTrend directionality: ___
  Aprovado (Trend > Range)? [SIM/NÃO]

Tarefa 2 — Grid IbBreakout:
  Dias filtrados: ___/___
  OOSValidated: ___
  Melhor Score IS: ___
  Melhor NetPnL OOS: ___

Tarefa 3 — Walk-forward IbBreakout:
  Janelas OOS positivas: ___/3

Tarefa 4 — SchoolRun comparativo:
  OOSValidated com filtro: ___
  OOSValidated sem filtro: ___
```

---

## O que NÃO alterar nesta sessão

- **Lógica de entrada/saída das outras strategies** — fora de escopo
- **Score do grid search** — não alterar a fórmula
- **ExecutionSettings defaults** — parâmetros MNQ estão corretos
- **Testes existentes** — 68/68 deve continuar passando

---

## Atualizar ao concluir

Após confirmar os resultados, atualizar `ChatMD/CLAUDE.md`:
- Marcar como ✅ "Validação estatística do IB classifier"
- Marcar como ✅ "Validar/tunar IbBreakout" (se grid aprovado)
- Atualizar contagem de testes
- Atualizar seção de próximos passos removendo itens concluídos

Commitar com mensagem:
```
feat(validation): IB classifier empirical validation + IbBreakout grid search
```
