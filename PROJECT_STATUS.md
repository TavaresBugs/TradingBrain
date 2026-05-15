# Project Status

Atualizado em: 2026-05-15

## Resumo

`TradingBrain` e um playground limpo em `.NET 8` para reconstruir, testar e comparar estrategias inspiradas nos metadados da `NinjaBotIA.dll`, sem executar a DLL vendor bloqueada.

## Estado atual

- Fase 1 - Fundacao profissional: concluida em 2026-05-15.
- Fase 2 - Realismo de execucao: concluida em 2026-05-15.
- Suite inicial de testes unitarios criada em `tests/TradingBrain.Tests`.
- CI GitHub Actions criado em `.github/workflows/dotnet.yml`.
- `DecisionEngine` usa regras injetaveis via `IDecisionRule`.
- `ExecutionSettings` valida invariantes no proprio tipo de dominio.
- Manifesto de rodada `run-manifest.json` criado junto dos outputs.
- Convencoes de editor e formatacao registradas em `.editorconfig`.
- `Volatility` atualizada para a especificacao v2 com filtros VWAP/RSI, expansao ATR/range e saidas hierarquicas.


- A solucao compila com `dotnet build .\TradingBrain.slnx`.
- A estrutura foi achatada e organizada na raiz do pacote.
- O codigo principal fica em `src`.
- A documentacao principal fica em `docs`.
- Os resultados gerados ficam em `outputs`.
- O NinjaScript limpo e gerado sob demanda em `generated`, fora do versionamento.

## Capacidades prontas

- Leitura de CSV padrao e export NinjaTrader.
- Backtest offline das strategies reconstruidas.
- Export de sinais por candle.
- Export de trades fechados.
- Resumo probabilistico por strategy.
- Grid search simples por parametros.
- Custos de execucao com slippage, spread, comissao, tick size, tick value e quantity.
- Inspecao estatica read-only da DLL.
- Geracao sob demanda de NinjaScript limpo.
- Testes unitarios para indicadores tecnicos, parser CSV e `DecisionEngine`.
- Manifesto JSON com dataset, parametros, custos, versao do codigo e arquivos gerados.
- Grid search de `Volatility` cobre as tres ondas principais de calibracao da v2.

## Strategies reconstruidas

- `Volatility`
- `GoldBreakout`
- `Momentum`
- `Range`
- `Trend`
- `Ema`

## Evidencias principais

- A DLL original possui metadados uteis de tipos, fields e properties.
- `OnStateChange` e `OnBarUpdate` aparecem como `ret-only` nos relatorios estaticos.
- As regras atuais sao hipoteses limpas baseadas nos metadados, nao clones comprovados da DLL bloqueada.

## Arquivos de entrada

- Status do projeto: este arquivo.
- Plano de evolucao: `ROADMAP.md`.
- Documentacao navegavel: `docs\README.md`.
- Espinha dorsal tecnica: `docs\00-visao-geral-e-espinha-dorsal.md`.
- Inventario de src: `docs\23-inventario-src-api-legado.md`.

## Limitacoes atuais

- A execucao assume entrada/saida no fechamento do candle.
- Grid search ainda nao tem split treino/teste ou walk-forward.
- Indicadores ainda podem ser otimizados para grids grandes.
- Cobertura ainda e inicial; backtester e estrategias reconstruidas precisam de testes adicionais.
