# Volatility v2

Status: implementada no playground em 2026-05-15.

## Regra

`Volatility` agora representa um breakout por expansao de volatilidade com confirmacao de tendencia curta:

- EMA9 acima/abaixo da EMA21 define direcao.
- VWAP exige distancia minima configuravel.
- RSI permite momentum entre 50-70 para long e 30-50 para short.
- Expansao pode usar ATR, candle range ou ambos.
- Volume deve estar acima da media por ratio configuravel.
- Squeeze e opcional, nao requisito base.

## Saidas

A hierarquia implementada e:

1. Fechamento forçado fora da janela 09:30-10:10.
2. `MaxDrawdownPoints`.
3. Stop fixo por ATR.
4. Chandelier por fechamento, em modo VWAP/EMA ou ATR.
5. RSI extremo apenas antes do trailing ativar.
6. Tempo maximo sem lucro minimo.

## Parametros

Os campos da v2 ficam em `StrategyTuningParams`:

- `VolatilityMinAtrRatio`
- `VolatilityMinVolumeRatio`
- `UseSqueezeFilter`
- `VolatilitySqueezeRatio`
- `VolatilityRangeMultiplier`
- `VolatilityExpansionMode`
- `VwapMinDistance`
- `RsiLongMax`
- `RsiShortMin`
- `VolatilityTrailingMode`
- `AtrChandelierMultiplier`
- `MaxBarsWithoutProfit`
- `MinProfitAtrRatio`
- `AtrStopMultiplier`
- `TrailingActivationBars`

O manifesto `run-manifest.json` serializa estes parametros junto dos custos e arquivos gerados.
