# Project Status

Atualizado em: 2026-05-15

## Resumo

`TradingBrain` e um playground limpo em `.NET 8` para reconstruir, testar e comparar estrategias inspiradas nos metadados da `NinjaBotIA.dll`, sem executar a DLL vendor bloqueada.

## Estado atual

- Fase 1 - Fundacao profissional: concluida em 2026-05-15.
- Fase 2 - Realismo de execucao: concluida em 2026-05-15.


- A solucao compila com `dotnet build .\TradingBrain.slnx`.
- A estrutura foi achatada e organizada na raiz do pacote.
- O codigo principal fica em `src`.
- A documentacao principal fica em `docs`.
- Os resultados gerados ficam em `outputs`.
- O NinjaScript limpo gerado fica em `generated`.

## Capacidades prontas

- Leitura de CSV padrao e export NinjaTrader.
- Backtest offline das strategies reconstruidas.
- Export de sinais por candle.
- Export de trades fechados.
- Resumo probabilistico por strategy.
- Grid search simples por parametros.
- Custos de execucao com slippage, spread, comissao, tick size, tick value e quantity.
- Inspecao estatica read-only da DLL.
- Geracao de `generated\CleanNinjaBotIAReconstruction.cs`.

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
- Ainda nao ha suite de testes unitarios.
