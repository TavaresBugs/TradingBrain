# Guia rapido: extrair logica de uma Strategy pelo dnSpy

Este fluxo serve para entender a logica de uma strategy sem executar o NinjaTrader e sem depender da licenca inicializar.

## 1. Abrir a DLL

1. Abra o dnSpy.
2. Use `File > Open`.
3. Selecione a DLL da strategy, por exemplo `NinjaBotIA.dll`.
4. Aguarde a arvore de tipos carregar.

## 2. Encontrar a classe da strategy

Procure uma classe que:

- herda de `NinjaTrader.NinjaScript.Strategies.Strategy`;
- contem `OnStateChange`;
- contem `OnBarUpdate`.

No C# decompilado, a assinatura costuma aparecer parecida com:

```csharp
public class NinjaBotIA : Strategy
```

Mesmo com ofuscacao, `OnBarUpdate` e `OnStateChange` normalmente continuam reconheciveis porque o runtime do NinjaTrader precisa chamá-los.

## 3. Ler `OnStateChange`

O objetivo aqui e mapear o que cada campo representa.

Procure trechos parecidos com:

```csharp
if (State == State.DataLoaded)
{
    this.a = SMA(20);
    this.b = SMA(50);
    this.c = RSI(14, 3);
}
```

Anote:

- campo `a` = `SMA(20)`;
- campo `b` = `SMA(50)`;
- campo `c` = `RSI(14, 3)`.

## 4. Ler `OnBarUpdate`

O objetivo aqui e mapear condicoes e acoes.

Procure:

- comparacoes: `>`, `<`, `>=`, `<=`, `==`;
- acessos `[0]`, `[1]`, `Close[0]`, `High[0]`, indicadores;
- chamadas de ordem: `EnterLong`, `EnterShort`, `ExitLong`, `ExitShort`, `SetStopLoss`, `SetProfitTarget`;
- filtros: `CurrentBar`, horario, volume, posicao, quantidade maxima de trades.

Exemplo de traducao:

```csharp
if (this.a[0] > this.b[0] && this.c[0] < 30.0)
    EnterLong(1);
```

vira:

```text
Entrada Long:
- SMA curta acima da SMA longa
- RSI abaixo de 30
- quantidade 1
```

## 5. Usar Find References

Para cada campo ofuscado:

1. Clique no campo, por exemplo `this.a`.
2. Botao direito.
3. `Analyze` ou `Find References`.
4. Veja onde ele e atribuido e onde e lido.

Isso resolve a maior parte do mapa `campo -> indicador`.

## 6. Reconstruir o pseudocodigo

Use a planilha `90-template-fluxo-strategy.csv` deste pacote.

Preencha uma linha por decisao:

- metodo;
- bloco/ordem;
- indicadores usados;
- condicao;
- acao;
- observacoes;
- confianca.

## 7. Extrair para o motor isolado

Depois que a decisao estiver clara, mova a regra para uma classe C# pura:

```csharp
public DecisionResult Evaluate(DecisionInput input)
{
    if (input.ShortAverage > input.LongAverage && input.Rsi < 30)
        return DecisionResult.Buy("SMA curta > longa e RSI < 30");

    return DecisionResult.None();
}
```

O wrapper do NinjaTrader deve apenas calcular/ler os valores e chamar o motor.
