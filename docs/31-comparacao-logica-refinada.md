# Refined Logic Comparison

Comparacao entre o harness anterior e a primeira implementacao dos filtros sugeridos: entrada mais seletiva, stops ATR e saidas adaptativas.

## Resultado MNQ

Arquivo de dados: `C:\Users\jhonv\OneDrive\Documentos\MNQ 06-26.Last.txt`

| Strategy | Baseline Trades | Baseline PF | Baseline Expectancy | Baseline Net | Refined Trades | Refined PF | Refined Expectancy | Refined Net | Leitura |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Momentum | 74 | 1.4835 | 2.6791 | 198.25 | 47 | 1.4426 | 3.1596 | 148.50 | Melhorou expectancy, reduziu trades e net. Continua promissora. |
| Ema | 227 | 1.1574 | 2.2104 | 501.75 | 237 | 1.1885 | 1.8586 | 440.50 | PF subiu um pouco, expectancy/net cairam. Precisa calibrar trailing/RSI. |
| Trend | 24 | 0.6903 | -8.2188 | -197.25 | 11 | 0.6138 | -8.25 | -90.75 | Continua negativa; refinamento so reduziu exposicao. |
| Volatility | 18 | 1.4223 | 3.0972 | 55.75 | 0 | 0 | 0 | 0 | Filtro de squeeze/volume ficou restritivo demais. |
| Range | 7 | 1.4007 | 7.6429 | 53.50 | 0 | 0 | 0 | 0 | Compressao + banda de 4 ATR ficou restritiva demais. |
| GoldBreakout | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | Continua sem trades no MNQ com a janela atual. |

## Conclusoes

- A ideia de adicionar stops ATR e filtros de forca e valida, mas nao deve ser aplicada com thresholds fixos sem calibracao.
- `Momentum` foi o melhor candidato do refinamento: menos trades, expectancy maior, drawdown ligeiramente menor.
- `Volatility` e `Range` precisam de filtros mais graduais, nao binarios:
  - reduzir `minVolumeRatio`;
  - relaxar squeeze/compressao;
  - usar ranking/score em vez de bloquear totalmente a entrada.
- `Trend` precisa de revisao mais profunda da premissa de entrada no MNQ.
- `GoldBreakout` parece desalinhada com o ativo/horario; provavelmente era desenhada para outro instrumento ou sessao.

## Proximo passo recomendado

Criar parametros por estrategia para calibrar:

- `VolatilityMinAtrRatio`
- `VolatilityMinVolumeRatio`
- `UseSqueezeFilter`
- `RangeCompressionRatio`
- `MomentumMinMacdAtrRatio`
- `EmaVolumeRatio`
- `AtrStopMultiplier`

Depois rodar grid search pequeno e walk-forward para evitar overfit.
