# Documentacao Tecnica - TradingBrain

Este indice centraliza a documentacao por camada de responsabilidade. A nomenclatura numerica existe para manter a leitura em ordem no Explorer.

## 00 - Visao Geral

- `00-visao-geral-e-espinha-dorsal.md`: arquitetura, fluxo de dados, relacao com a DLL bloqueada e principios de reconstrucao.

## 10 - Evidencias e Metadados

- `10-evidencia-dll-inspecao-estatica.md`: relatorio gerado por `--inspect-dll`; contem tipos, fields, properties e metodos `ret-only`.
- `11-evidencia-dll-achados-iniciais.md`: resumo inicial dos achados da DLL.
- `12-evidencia-dll-probe-runtime.txt`: log historico de diagnostico do playground/probe.

## 20 - Reconstrucao e Comparacao

- `20-mapa-reconstrucao-strategies.md`: correspondencia entre metadados da DLL, defaults e regras reconstruidas.
- `21-comparacao-dll-vs-reconstrucao.md`: limites da comparacao entre DLL bloqueada e codigo limpo.
- `22-plano-automacao-reconstrucao.md`: plano historico de automacao da reconstrucao.
- `23-inventario-src-api-legado.md`: inventario da API atual e arquivos legados em `src`.

## 30 - Validacao Quantitativa

- `30-harness-probabilidade.md`: harness de probabilidade, CSVs gerados e metricas financeiras.
- `31-comparacao-logica-refinada.md`: comparacao baseline vs regras refinadas no MNQ.
- `32-grid-search-parametros.md`: camada de parametros e grid search.

## 40 - Guias Operacionais

- `40-guia-dnspy-extracao.md`: guia manual historico de leitura no dnSpy.

## 90 - Templates e Referencias

- `90-template-fluxo-strategy.csv`: template antigo para mapear fluxo de strategy manualmente.

## Regra de Manutencao

Quando uma nova descoberta aparecer, atualize primeiro `00-visao-geral-e-espinha-dorsal.md`. Os demais arquivos podem ser detalhados, historicos ou experimentais, mas a espinha dorsal deve continuar sendo o mapa confiavel do projeto.