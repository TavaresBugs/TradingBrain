# Espinha Dorsal da Reconstrucao NinjaBotIA

Data base: 2026-05-15

Este arquivo e a fonte principal da verdade do projeto. Ele conecta os relatorios soltos, o codigo limpo, os testes no MNQ e o que ainda falta implementar.

## Objetivo

Construir um playground profissional para estudar, reconstruir, testar e melhorar estrategias inspiradas na DLL `NinjaBotIA.dll`, sem depender da DLL vendor bloqueada para execucao.

O objetivo pratico nao e burlar a DLL. E transformar o que conseguimos observar de forma estatica em codigo limpo, testavel e evolutivo.

## Estado atual em uma frase

Temos metadados suficientes para reconstruir a arquitetura das strategies e um backtester funcional para testar hipoteses, mas nao temos o IL original dos metodos de decisao porque `OnStateChange` e `OnBarUpdate` aparecem como corpos `ret-only`.

## Evidencia coletada da DLL

DLL analisada:

```text
C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\bin\Custom\NinjaBotIA.dll
```

Relatorio principal:

```text
docs\10-evidencia-dll-inspecao-estatica.md
```

Achados confirmados:

- A DLL referencia `NinjaTrader.Core`, `NinjaTrader.Gui` e `NinjaTrader.Vendor` versao `8.1.5.2`.
- A DLL possui `<Module>.cctor`.
- O inicializador do modulo chama runtime AgileDotNet.
- Foram detectados 6 tipos de strategy:
  - `ema`
  - `NinjaBotIAGoldBreakout_v1_0_0_0`
  - `NinjaBotIAMomentum_v1_0_0_0`
  - `NinjaBotIARange_v1_0_0_0`
  - `NinjaBotIATrend_v1_0_0_1`
  - `NinjaBotIAVolatility_v1_0_0_0`
- Os fields e properties estao preservados e sao a principal base da reconstrucao.
- `OnStateChange` e `OnBarUpdate` aparecem com 1 instrucao IL e corpo `ret-only`.
- Nao foram encontradas chamadas de ordem dentro desses corpos, porque os corpos gerenciados visiveis foram esvaziados/protegidos.

Observacao importante:

Alguns relatorios antigos indicaram "nenhuma estrategia detectada". Isso nao invalida o resultado atual; era uma limitacao do detector usado naquele momento. O relatorio `10-evidencia-dll-inspecao-estatica.md` e o mapa de reconstrucao sao as referencias mais recentes para os tipos detectados.

## O que a DLL nos deu de util

Os campos indicam a planta baixa de cada strategy:

| Strategy | Evidencia mais forte | O que reconstruimos |
|---|---|---|
| `Volatility` | EMA1/EMA2, RSI, VWAP, SMA volume, ATR, trailing, breakeven, time exit, max drawdown | Filtro de tendencia + volatilidade + volume + gestao por ATR |
| `Momentum` | MACD, series internas, MA kind, cloud/arrows, stop/target, time filter | Momentum com MACD/RSI/EMA e saidas por reversao/tempo |
| `Range` | range filter, ATR, swing period/multiplier, TP/SL por ATR | Rompimento/retorno de faixa com filtro ATR |
| `Trend` | up/down series, trend series, SMA TR, ATR multiplier, break-even, trailing | Trend following com banda/estado e gestao dinamica |
| `GoldBreakout` | janela de horario, high da janela, partial, take1/take2, stop, close all | Breakout intradiario por janela de abertura |
| `ema` | Swing, Stop, Target | EMA/swing simplificada com stop/target |

## O que e codigo nosso

Raiz do projeto:

```text
C:\Users\jhonv\OneDrive\Desktop\NinjaBotIA_Reconstruction_Package
```

Codigo principal:

```text
src\TradingBrain.Core
src\TradingBrain.Console
```

Arquivos mais importantes:

```text
src\TradingBrain.Core\BacktestModels.cs
src\TradingBrain.Core\TechnicalIndicators.cs
src\TradingBrain.Console\StrategyBacktester.cs
src\TradingBrain.Console\StrategyRules.cs
src\TradingBrain.Console\BacktestReports.cs
src\TradingBrain.Console\GridSearchRunner.cs
src\TradingBrain.Console\Program.cs
```

Codigo NinjaTrader limpo gerado:

```text
generated\CleanNinjaBotIAReconstruction.cs, gerado sob demanda e ignorado pelo Git
```

Esse arquivo nao depende da DLL bloqueada. Ele e uma reconstrucao limpa baseada em metadados + hipoteses testadas no playground.

## O que ja foi testado

Dataset usado nos primeiros testes:

```text
C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt
```

Resultado baseline no MNQ:

| Strategy | Trades | ProfitFactor | Expectancy | NetPnL | Leitura |
|---|---:|---:|---:|---:|---|
| Volatility | 18 | 1.4223 | 3.0972 | 55.75 | Promissora, mas pouca amostra |
| Momentum | 74 | 1.4835 | 2.6791 | 198.25 | Melhor candidata inicial |
| Range | 7 | 1.4007 | 7.6429 | 53.50 | Pouca amostra |
| Ema | 227 | 1.1574 | 2.2104 | 501.75 | Muito ativa, precisa controle de risco |
| Trend | 24 | 0.6903 | -8.2188 | -197.25 | Desalinhada com MNQ atual |
| GoldBreakout | 0 | 0 | 0 | 0 | Janela/ativo desalinhados |

Resultado apos primeira rodada refinada:

- `Momentum` melhorou expectancy e reduziu ruido.
- `Ema` melhorou pouco o ProfitFactor, mas perdeu expectancy.
- `Trend` continuou negativa.
- `Volatility` e `Range` ficaram restritivas demais e zeraram trades.
- `GoldBreakout` continuou sem trades no MNQ.

Conclusao atual:

`Momentum` e `Ema` sao as duas frentes mais uteis para evoluir primeiro. `Volatility` e `Range` precisam de filtros graduais/parametrizados. `Trend` e `GoldBreakout` dependem de recalibracao forte por ativo/sessao.

## Nivel de confianca por componente

| Componente | Confianca | Motivo |
|---|---|---|
| Lista de strategies | Alta | Confirmada por metadados |
| Campos/properties | Alta | Confirmados por metadados |
| Defaults e ranges visiveis | Media/Alta | Extraidos por attributes quando disponiveis |
| Corpo real de `OnBarUpdate` | Baixa | Corpo visivel esta `ret-only` |
| Regras reconstruidas | Media | Hipoteses coerentes com campos e resultados |
| Resultados MNQ | Media | Rodada real, mas uma amostra/ativo |
| Codigo NinjaScript limpo | Media | Compativel conceitualmente, mas deve ser validado no NT8 |

## Como rodar hoje

Build:

```powershell
dotnet build .\TradingBrain.slnx
```

Rodar todas as strategies:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --run-all "C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt" .\outputs\probability-harness-output
```

Grid search:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --grid-search "C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt" .\outputs\grid-search-output Momentum
```

Gerar NinjaScript limpo:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --generate-ninja .\generated
```

Inspecionar DLL estaticamente:

```powershell
dotnet run --project .\src\TradingBrain.Console\TradingBrain.Console.csproj -- --inspect-dll "C:\Users\jhonv\OneDrive\Documentos\NinjaTrader 8\bin\Custom\NinjaBotIA.dll" .\docs\10-evidencia-dll-inspecao-estatica.md
```

## Roadmap de implementacao

### 1. Organizar a verdade do projeto

- Manter este arquivo atualizado.
- Tratar `10-evidencia-dll-inspecao-estatica.md` como relatorio de evidencia.
- Tratar `31-comparacao-logica-refinada.md` como resultado experimental, nao como verdade final.

### 2. Melhorar qualidade do backtester

- Adicionar custo por trade: spread/slippage/comissao.
- Separar in-sample e out-of-sample.
- Exportar equity curve consolidada.
- Evitar recalculos caros de indicadores em loops longos.

### 3. Evoluir regras por strategy

- Parametrizar thresholds em vez de hardcode.
- Transformar filtros binarios em score quando fizer sentido.
- Criar harness especifico para `Momentum` e `Ema` primeiro.
- Recalibrar `Volatility` e `Range` com filtros menos restritivos.
- Reavaliar `Trend` e `GoldBreakout` apenas depois de ajustar ativo/sessao.

### 4. Validar robustez

- Rodar MNQ em mais periodos.
- Testar NQ/ES/MES/GC quando houver CSV.
- Rodar walk-forward.
- Comparar baseline vs refined vs grid winner.

### 5. Gerar codigo limpo final

- Congelar parametros vencedores por strategy.
- Gerar NinjaScript limpo.
- Validar compilacao no NinjaTrader.
- Manter adaptador fino e motor separado quando possivel.

## Proximas decisoes tecnicas

As decisoes mais importantes agora sao:

1. Escolher se o foco inicial sera `Momentum` ou `Ema`.
2. Definir custos realistas para MNQ.
3. Rodar grid search completo com train/test separado.
4. Gerar `CleanNinjaBotIAReconstruction.cs` localmente apenas depois de estabilizar as regras.

Minha recomendacao tecnica atual: focar em `Momentum` como estrategia-ancora e usar `Ema` como estrategia de alta frequencia para comparacao. Volatility e Range devem voltar depois com filtros graduais.
