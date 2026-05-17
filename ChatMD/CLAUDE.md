# TradingBrain — CLAUDE.md

> Leia este arquivo antes de qualquer sessão. Ele é a fonte única de verdade sobre o estado do projeto.  
> Última atualização: 2026-05-17 | Testes: 74+ | Dataset: mnq_5m_12mo.csv (82.659 barras, mar/2025–mai/2026)

---

## O que é o projeto

Engine de backtest próprio em C# (.NET 10) para MNQ (Micro Nasdaq Futures). O objetivo é reconstruir, validar e refinar strategies de futuros usando um pipeline completamente independente — sem NinjaTrader, sem DLL externa, rodando sobre CSVs de barras OHLCV.

**Repositório:** https://github.com/TavaresBugs/TradingBrain

---

## Stack e comandos essenciais

```bash
dotnet build ./TradingBrain.slnx
dotnet test ./TradingBrain.slnx

# Parâmetros obrigatórios para MNQ (sempre incluir):
--tick-size 0.25 --tick-value 0.50 --slippage-ticks 1 --spread-ticks 1 --commission-per-side 0.62

# Comandos disponíveis:
dotnet run -- <bars.csv> <output_dir> [--strategy <nome>]
dotnet run -- --grid-search <bars.csv> <output_dir> [Strategy] [--no-regime-filter]
dotnet run -- --walk-forward <bars.csv> <output_dir> Strategy --windows N
dotnet run -- --classify-regime <bars.csv> <output_dir>
dotnet run -- --run-all <bars.csv> <output_dir>
dotnet run -- --replay <signals.csv> [--delay 200]
```

**Dataset principal:** `outputs/tv-bars/mnq_5m_12mo.csv` (não versionado)

---

## Estado atual — o que está validado e funcionando

### Pipeline completo ✅
Grid search IS/OOS (65/35), walk-forward rolling, classify-regime, run-all, replay, análise de excursão (MAE/MFE por trade), regime filter no grid e walk-forward. Tudo operacional.

### 10 strategies implementadas ✅
Volatility, Momentum, Ema, Trend, Range, OrbBreakout, VwapReversion, BollingerFade, SchoolRun, IbBreakout.

### IB Classifier validado empiricamente ✅
Substituiu o KER-based (que tinha directionality invertido — Range > Trend). O IB (Initial Balance 9:30-10:30 ET) classifica cada dia com base no comportamento da sessão atual, não no histórico. Distribuição nos 82k dados:

```
Range            164 dias  (54.5%)   directionality 0.417
Trend             47 dias  (15.6%)   directionality 0.556  ✓ maior que Range
Breakout          37 dias  (12.3%)   directionality 0.540
Undefined         20 dias  ( 6.7%)   directionality —
NonTrend          20 dias  ( 6.7%)   directionality 0.054  ✓ quase zero
HighVolatility    12 dias  ( 4.0%)   directionality —
```

Hierarquia correta: Trend > Breakout > Range > NonTrend. Classificador aprovado.

### RegimeFilter no grid e walk-forward ✅
Cada strategy só roda nos dias do seu regime alvo. Flag `--no-regime-filter` para comparação. Gate mínimo: 30 dias após filtro. Log sempre impresso: `[RegimeFilter] Strategy: X/Y dias úteis`.

### Gestão de trade implementada ✅
Breakeven (BE) e chandelier trailing por strategy. Lógica em `StrategyRules.cs`:

- **Trend:** stop 3xATR, BE em +1R, chandelier 2xATR ativado em +0.75R. Timeout removido — chandelier assume esse papel. Resultado grid: NetPF 4.49, WinRate 61.9%.
- **Momentum:** stop 1.2xATR, BE em +0.75R, chandelier 2xATR ativado em +1.25R. Resultado grid: NetPF 2.33, WinRate 39.7%.
- **EMA:** stop ATR, BE em +0.5R, sem trailing. AvgR foi de +0.02 para +0.08.
- **Volatility:** MaxDrawdown hard cap removido, janela ampliada para 11:30. AvgR saiu do zero.
- **Range e BollingerFade:** sem BE, sem trailing. Target fixo configurável via `RangeTargetRatio` e `BbFadeTargetRatio`.

**Bug crítico corrigido:** chandelier stop é capped em entryPrice quando `beActivated = true`, evitando perda mesmo com expansão de ATR pós-ativação.

### Análise de excursão (4.157 trades) ✅
Dados em `outputs/run-all-excursion-validation/`. Revelou:

- **Trend:** MFER 2.25, hit +3R em 25.7% — runners reais. Timeout estava cortando esses trades.
- **IbBreakout:** RRGap -0.24 — alvo muito longe. Fix: `IbTargetMultiplier` reduzido + exit às 13:30.
- **Volatility:** 54.9% das saídas eram MaxDrawdown — hard cap destruía trades. Removido.
- **BollingerFade:** RRGap -0.03, marginal negativo — problema de setup, não de stop.

---

## O que está pendente de validação

### 🔴 Pendente — chandelier fix + range RR (próximo branch)
Branch: `feat/chandelier-fix-range-rr`

Dois trabalhos simultâneos:
1. **Fix do chandelier** (já documentado): `Math.Max(rawStop, entryPrice)` em Trend e Momentum quando BE ativo.
2. **RR configurável** para Range (`RangeTargetRatio`) e BollingerFade (`BbFadeTargetRatio`). Grid vai testar [0.8, 1.0, 1.2, 1.4]. Hipótese: 1.2 é o sweet spot — a matemática mostra que com 1.2:1 o break-even de win rate cai de 50% para 45.5%, e em range o preço passa por 1.0R no caminho para 1.2R.

**Métricas de sucesso:**
- Trend AvgR deve voltar para ~0.49+ (estava 0.4274 com defaults errados de BE=0.5R)
- Nenhum trade de chandelier com `GrossPoints < 0`
- Grid de Range com RR confirmando o valor ótimo empiricamente

### 🟡 Pendente — re-entradas após stop hunting
Contexto: o mercado (especialmente NQ em dias de tendência) frequentemente busca liquidez abaixo de swings antes de retomar a direção. Com stop de 3xATR a Trend já sobrevive à maioria das varreduras, mas stop menor + re-entrada seria mais eficiente em capital.

Antes de implementar, precisamos calcular: dos trades que foram stopados em "Stop dinâmico long/short" no Trend, quantos tinham o preço se recuperando na direção original nas próximas 3-5 barras? Se > 40%, vale implementar re-entrada com stop no mínimo da varredura.

**Não implementar ainda** — validar o dado de excursão primeiro.

### 🟡 Pendente — walk-forward com gestão de trade
O walk-forward atual usa os defaults do `StrategyTuningParams`. Os novos defaults de Trend (stop 3xATR, BE+1R, chandelier) precisam ser testados em walk-forward para ver se o OOS é consistente com o IS.

### 🟢 Backlog — mais dados
Dataset atual cobre mar/2025–mai/2026. Para validação estatística robusta do IB classifier e das strategies, 2+ anos (2024-2026) é o ideal. Baixar via TradingView 4 arquivos cobrindo 2024 e rodar `convert_tradingview.py`.

---

## Lógica e decisões de design — o "porquê" de cada escolha

### Classificador de regime: IB em vez de KER
O KER mede eficiência dos últimos 10 dias — é um sinal tardio que descreve o passado. O IB mede o comportamento da sessão atual antes das 10:30: se o preço abriu fora do IB de ontem e o IB de hoje é estreito, o mercado está confirmando tendência em tempo real. Metodologia derivada do Market Profile de Steidlmayer/CBOT, usada por prop desks institucionais.

### Stop de 3xATR na Trend
O grid testou 240 combinações. Stop 3xATR dominou com Score 1050 vs Score 98 para 2xATR. Motivo empírico: NQ em dias de tendência frequentemente faz varreduras de liquidez (stop hunting) antes de continuar. 2xATR não sobrevive à varredura; 3xATR sim. O WinRate saltou de 22.7% para 61.9% apenas mudando o stop.

### BE antes de trailing, não depois
O chandelier só dispara quando `beActivated = true`. Essa ordem garante que o sistema nunca perde dinheiro em trades que já foram a favor. O chandelier expande o lucro potencial; o BE garante o piso. Com BE em +1R e chandelier em +0.75R (Trend), o chandelier na prática dispara junto com o BE — o que significa que qualquer retração após +0.75R que volte ao entry sai pelo BE, e qualquer retração que não chegue ao entry é mantida pelo chandelier.

### Sem BE e sem trailing em Range e BollingerFade
Em mercados de range, traders institucionais usam targets fixos porque o preço oscila de banda a banda. BE aqui causaria saídas no zero-a-zero durante oscilações normais do range. O MFER de BollingerFade é 0.89 — trades quase nunca excedem 1R. Trailing em algo com MFER < 1 é contraproducente. Target fixo 1.2:1 matematicamente superior: break-even win rate de 45.5% em vez de 50%.

### Score do grid
```
Gate: ClosedTrades >= 30 E NetExpectancy > 0
pf         = Min(NetProfitFactor, 10.0)
rtd        = Clamp(ReturnToDrawdown, -5.0, 20.0)
confidence = Log10(ClosedTrades + 1)
Score      = pf * NetExpectancy * confidence * (1.0 + rtd * 0.1)
```
O `confidence` via Log10 penaliza grids com poucos trades sem torná-los inválidos. O `rtd` dá bônus a sistemas que recuperam bem — mais útil que só PF isolado.

### Ordem de operações no filtro de regime
Classificação sempre em `allBars` completo → filtra por regime → divide em IS/OOS. Nunca o contrário. Motivo: ATR14 dos primeiros dias de uma janela pequena não tem contexto suficiente para calcular o IB de ontem corretamente.

---

## Mapeamento strategy → regime (fixo, não alterar sem justificativa)

| Strategy | Regimes | Lógica |
|---|---|---|
| Trend | Trend + Breakout + WideIbBreakout + IntradayExpansion | Donchian 10 + RSI + stops largos |
| Momentum | Trend | MACD cross + EMA + volume; Breakout/Wide removidos por atraso do MACD 5m |
| Ema | WideIbBreakout + IntradayExpansion + HighVolatility | EMA9>21 + swing rompido + volume; validada em regimes de expansao real |
| SchoolRun | Trend + Breakout + WideIbBreakout + IntradayExpansion | Candle M15 de referência + overnight range; alinhado ao ORB para comparar rompimentos direcionais |
| OrbBreakout | Trend + Breakout + WideIbBreakout + IntradayExpansion | ORB 9:30-10:00; edge em regimes direcionais/expansao, Range negativo |
| IbBreakout | Trend + Breakout + WideIbBreakout + IntradayExpansion + HighVolatility | Range IB 9:30-10:30; edge forte em regimes direcionais/expansao, Range negativo |
| VwapReversion | Range + HighVolatility | Reversão ao VWAP + RSI extremo |
| BollingerFade | Range | Toque na banda Bollinger + reversão |
| Range | Range | Strategy solo excluida dos reports/grids agregados; VwapReversion e BollingerFade cobrem Range |
| Volatility | Breakout + WideIbBreakout + IntradayExpansion | ATR expansion + squeeze + volume |

`NonTrend` e `Limbo` → sempre excluídos. `Undefined` → excluído por padrão.

### Regime Limbo + Range por rejeição

Calibração em 2026-05-17 (`feat/regime-limbo`):

- Base empírica usada: IB só vira expansão quando há aceitação fora do IB; rejeição/retorno para dentro é rotação. Fechamento no miolo do range e cruzamentos recorrentes de VWAP são tratados como comportamento de Range. Dias sem aceitação direcional e sem rotação limpa ficam em `Limbo`.
- Métricas adicionadas ao `DayRegime`: `DayRangeAtr`, `CloseLocation`, `DirectionalEfficiency`, `IbExtensionAtr`, `CloseOutsideIb`, `BrokeBothIbSides`, `VwapCrossCount`.
- Distribuição MNQ 12m após ajuste: `Range=151/226 (66.8%)`, `Limbo=30/226 (13.3%)`, `Trend=18`, `Breakout=14`, `WideIbBreakout=4`, `HighVolatility=3`, `IntradayExpansion=2`, `Undefined=4`.
- Full-report vs `outputs/full-report-regime-fix-final`: trades `887 -> 861`, WinRate `50.77% -> 54.68%`, NetCurrency `$28,122.12 -> $29,382.86`.
- Melhoras grandes: VwapReversion `$1,881.86 -> $6,029.96`, BollingerFade `$772.50 -> $1,498.32`, Momentum `$1,828.98 -> $2,917.34`.
- SRS com Range amplo caiu `$1,759.96 -> $690.18`; remover `Range` do mapa recuperou qualidade: trades `168 -> 17`, WinRate `47.62% -> 76.47%`, NetPF `1.162 -> 8.140`, NetCurrency `$690.18 -> $1,313.42`. Amostra ainda pequena e OOS zerado, mas confirma que SRS nao deve operar em Range rotacional.
- Experimento direcional para IB/ORB em 2026-05-17: abrir `IbBreakout` para `Trend+Breakout+WideIbBreakout+IntradayExpansion+HighVolatility` elevou trades `14 -> 40` e NetCurrency `$4,200.14 -> $9,673.40` mantendo NetPF `11.80 -> 12.65`. Abrir `OrbBreakout` para `Trend+Breakout+WideIbBreakout+IntradayExpansion` elevou trades `13 -> 30` e NetCurrency `$5,522.88 -> $9,228.80` com NetPF `Infinity -> 18.97`. `HighVolatility` ficou fora do ORB por amostra negativa.
- Experimento SRS/ORB overnight em 2026-05-17: SRS passou a usar os mesmos regimes do ORB (`Trend+Breakout+WideIbBreakout+IntradayExpansion`) para comparacao justa. Anti-mode agora e regra fixa quando a vela M15 de referencia fecha dentro do overnight; `UseAntiMode` removido. A vela de referencia agora e a N-esima M15 da sessao regular, com filtro `SrsMinRangeAtrRatio`; stop pode ancorar em high/low da vela de referencia (`SrsUseRefCandleStop=true`). Cache diario de ref candle + overnight evita travamento no grid. Resultado grid SRS corrigido: 22 trades IS, WinRate 54.55%, NetPF 2.85, NetCurrency `$1,689.72`, OOS 14 trades, NetCurrency `$1,253.64`, `OOSValidated=3`. HTML: `outputs/compare/srs-corrigido/dashboard.html`.
- SRS agora segue janela de entrada estilo ORB: apos o fechamento da vela M15 de referencia, novas entradas so sao permitidas por 1 hora. Resultado grid com janela: 21 trades IS, WinRate 52.38%, NetPF 2.73, NetCurrency `$1,585.96`; OOS manteve 14 trades e `$1,253.64`. Full-report default melhorou SRS de 300 trades, NetPF 1.043, NetPts 402.00 para 277 trades, NetPF 1.056, NetPts 494.26. HTML: `outputs/compare/srs-entry-window/dashboard.html`; full-report: `outputs/full-report-srs-entry-window/`.

---

## Regras para o agente — não negociáveis

**Compatibilidade:** nunca quebre assinaturas públicas existentes. Novos parâmetros ao final do record com default = comportamento atual. Novos valores de enum só por adição, nunca remoção.

**Testes:** os testes existentes continuam passando após qualquer mudança. Novos testes em arquivo separado. Reportar contagem final após `dotnet test`.

**Lookahead:** a classificação de regime usa `allBars` completo. Nunca classifique dentro de uma janela IS ou OOS isolada.

**NonTrend:** sempre excluído de todas as strategies, sem exceção.

**Chandelier + BE:** o chandelier stop é sempre `Math.Max(rawStop, entryPrice)` para long e `Math.Min(rawStop, entryPrice)` para short quando `beActivated = true`. Isso é inegociável — garante que `GrossPoints >= 0` em todos os trades de chandelier com BE ativo.

**Commit e push:** ao finalizar qualquer sessão, executar `git add -A && git commit -m "..." && git push origin <branch>` e confirmar que o push foi aceito.

---

## Arquitetura — onde está cada coisa

```
src/TradingBrain.Core/
  BacktestModels.cs       ← StrategyTuningParams (todos os params de tuning aqui)
  MarketRegime.cs         ← enum MarketRegime + record DayRegime (campos IB incluídos)
  RegimeClassifier.cs     ← IB-based, classifica por Initial Balance 9:30-10:30
  RegimeFilter.cs         ← Apply() filtra barras por regime; CountDaysByRegime() para diagnóstico
  StrategyRegimeMap.cs    ← mapeamento fixo StrategyKind → []MarketRegime
  PrecomputedSeries.cs    ← todos os indicadores pré-computados O(n)
  TechnicalIndicators.cs  ← EMA, ATR, MACD, RSI, VWAP, Bollinger, Resample...

src/TradingBrain.Console/
  StrategyBacktester.cs   ← loop principal, extrai trades, calcula métricas
  StrategyRules.cs        ← Evaluate* por strategy + helpers de gestão (BE, chandelier)
  GridSearchRunner.cs     ← grid IS/OOS com regime filter, Score, gates
  WalkForwardValidator.cs ← walk-forward rolling com regime filter
  BacktestReports.cs      ← CSVs de saída, summarize, regime distribution
  ReplayViewer.cs         ← viewer barra a barra para --replay
  TradeAnalyzer.cs        ← análise de excursão MAE/MFE, RMultiple, hit rates
  Program.cs              ← parsing de todos os comandos CLI
```

---

## Próximos passos em ordem de prioridade

1. **`feat/chandelier-fix-range-rr`** — fix do chandelier + RR configurável para Range e BollingerFade. Grid vai revelar se 1.2:1 é realmente o sweet spot. *Prompt gerado e pronto para execução.*

2. **Walk-forward com novos params** — após confirmar o chandelier fix, rodar walk-forward de Trend e Momentum com os params ótimos do grid (stop 3xATR, BE+1R, chandelier+0.75R para Trend; stop 1.2xATR, BE+0.75R, chandelier+1.25R para Momentum).

3. **Calcular % de stop-outs recuperados** — analisar os `*.trades.csv` para descobrir quantos trades stopados em Trend tinham preço se recuperando nas próximas 3-5 barras. Se > 40%, implementar re-entrada com stop no mínimo da varredura.

4. **Mais dados (2024)** — baixar via TradingView para ter 2+ anos e validar tudo com significância estatística real.

---

*Arquivo gerado em 2026-05-17. Atualize após cada sessão concluída.*
