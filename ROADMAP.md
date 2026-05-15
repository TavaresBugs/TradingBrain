# Roadmap

Este roadmap transforma o `TradingBrain` de playground funcional em laboratorio profissional de estrategia: mais confiavel, mais rapido, mais auditavel e mais facil de manter.

## Principios

- Manter poucas pastas e poucos arquivos por responsabilidade.
- Separar evidencia da DLL, hipotese reconstruida e resultado empirico.
- Evitar hardcode quando o valor precisa ser calibrado.
- Toda melhoria quantitativa precisa gerar CSV ou resumo comparavel.
- Nenhuma dependencia da DLL bloqueada para executar backtests.

## Fase 1 - Fundacao profissional

Prioridade: alta

Objetivo: deixar o projeto previsivel para qualquer pessoa abrir e entender.

Tarefas:

- Manter `PROJECT_STATUS.md` como foto atual curta.
- Manter `ROADMAP.md` como plano de execucao.
- Manter `docs\00-visao-geral-e-espinha-dorsal.md` como fonte principal da verdade tecnica.
- Padronizar nomes dos docs com prefixos numericos.
- Manter `src`, `docs`, `outputs`, `generated`, `adapters` e `probes` sem aninhamento desnecessario.

Pronto quando:

- A raiz do projeto cabe em uma tela.
- O README aponta para status, roadmap e docs.
- Um colaborador externo entende o projeto lendo 3 arquivos: `README.md`, `PROJECT_STATUS.md`, `ROADMAP.md`.

Status: concluida em 2026-05-15.

## Fase 2 - Realismo de execucao

Prioridade: alta

Objetivo: aproximar o backtest de uma execucao real.

Tarefas:

- Criar `ExecutionSettings` com validacao de invariantes.
- Parametrizar tick size, tick value, spread, slippage e comissao.
- Aplicar custos no PnL de cada trade.
- Registrar custos nos arquivos `*.trades.csv`.
- Adicionar campos de configuracao ao manifesto da rodada.

Pronto quando:

- Uma rodada mostra PnL bruto e PnL liquido.
- O mesmo dataset pode ser testado com diferentes custos.
- O resumo deixa claro se a strategy sobrevive apos custos.

Status: concluida em 2026-05-15.

## Fase 3 - Manifesto de rodada

Prioridade: alta

Objetivo: todo resultado gerado precisa ser reproduzivel.

Tarefas:

- [x] Criar `outputs\<run-name>\run-manifest.json`.
- [x] Salvar dataset, data/hora, strategy, parametros, custos e versao do codigo.
- [x] Criar subpastas por rodada nos exemplos principais.
- [x] Padronizar nomes de outputs gerados pelo runner.

Pronto quando:

- Qualquer CSV em `outputs` tem um manifesto correspondente.
- Da para saber exatamente com quais parametros um resultado foi gerado.

Status: concluida em 2026-05-15.

## Fase 4 - Validacao quantitativa robusta

Prioridade: alta

Objetivo: reduzir overfit e separar resultado bonito de vantagem real.

Tarefas:

- Adicionar split treino/teste.
- Adicionar walk-forward simples.
- Ordenar grid search por score ajustado por drawdown e numero minimo de trades.
- Exportar comparacao baseline vs refined vs grid winner.
- Adicionar filtros de amostra minima.

Pronto quando:

- Um grid search informa performance in-sample e out-of-sample.
- O resultado vencedor nao e escolhido apenas por NetPnL.
- Estrategias com poucos trades sao marcadas como baixa confianca.

## Fase 5 - Qualidade de codigo e testes

Prioridade: media/alta

Objetivo: evitar regressao enquanto mexemos nas regras.

Tarefas:

- [x] Criar projeto `TradingBrain.Tests` com testes de regressao.
- [x] Testar indicadores principais: EMA, SMA, ATR, RSI e VWAP.
- [x] Testar parser CSV.
- Testar extracao de trades.
- Testar regras simples de entrada/saida com datasets pequenos.
- Testar MACD quando houver API dedicada para o indicador.

Pronto quando:

- `dotnet test` valida a base matematica.
- Mudancas em regras nao quebram parser/export sem perceber.

## Fase 6 - Performance

Prioridade: media

Objetivo: deixar grid search rapido o suficiente para iterar.

Tarefas:

- Evitar `Take(i + 1).ToList()` em loops quentes.
- Criar calculo incremental para indicadores mais usados.
- Reaproveitar series calculadas entre strategies quando fizer sentido.
- Medir tempo de execucao por strategy/grid.

Pronto quando:

- Rodadas de grid deixam de ser gargalo para iteracao diaria.
- O codigo continua legivel, sem micro-otimizacao confusa.

## Fase 7 - Evolucao das strategies

Prioridade: media

Objetivo: melhorar regras com metodo, nao por tentativa solta.

Foco inicial:

- `Momentum`: candidata principal.
- `Ema`: estrategia ativa para comparacao e fluxo de trades.
- `Volatility`: atualizada para v2; precisa de validacao com dados MNQ reais.

Depois:

- `Volatility`: transformar filtros binarios em scores/parametros.
- `Range`: recalibrar compressao e banda.
- `Trend`: revisar premissa para MNQ.
- `OrbBreakout`: testar outros ativos/sessoes antes de descartar.

Pronto quando:

- Cada strategy tem parametros documentados.
- Cada mudanca tem comparacao antes/depois.
- Cada strategy tem classificacao: candidata, experimental ou arquivada.

## Fase 8 - NinjaScript limpo

Prioridade: media

Objetivo: gerar codigo limpo apenas depois de estabilizar regras.

Tarefas:

- Gerar `CleanNinjaBotIAReconstruction.cs` localmente com parametros vencedores.
- Manter nomes proximos dos metadados originais quando isso ajudar auditoria.
- Separar motor de decisao da casca NinjaTrader quando possivel.
- Validar compilacao no NinjaTrader.

Pronto quando:

- O arquivo gerado compila no NT8.
- O comportamento no playground e no NinjaScript limpo fica comparavel.

## Ordem recomendada agora

1. Finalizar Fase 1.
2. Implementar `ExecutionSettings` com validacao centralizada.
3. Criar manifesto de rodada.
4. Rodar Momentum e Ema com custos.
5. Adicionar treino/teste ao grid search.
6. So depois mexer forte em Volatility, Range, Trend e OrbBreakout.
