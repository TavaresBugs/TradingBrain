# Static Strategy Flow Mapper

Pacote com as tres pecas para comecar a ler uma strategy compilada:

- `Guide-dnSpy-Strategy-Extraction.md`: passo a passo manual no dnSpy.
- `StrategyFlowTemplate.csv`: planilha-base para mapear indicadores, condicoes e acoes.
- `StaticStrategyFlowMapperCommand.cs`: codigo do botao `F` para o plugin atual do dnSpy.

## Como instalar o botao F no plugin atual

1. Copie `StaticStrategyFlowMapperCommand.cs` para a pasta do projeto do plugin.
2. Garanta que ele fique no mesmo namespace:

```csharp
namespace VendorNotificationAuditPlugin;
```

3. Compile:

```powershell
dotnet build C:\Users\jhonv\OneDrive\Desktop\by\VendorNotificationAuditPlugin.csproj -c Release
```

4. Copie a DLL/PDB geradas para a pasta `Extensions` do dnSpy.

O comando vai aparecer no menu `_Audit` como:

```text
F - Static Strategy Flow Mapper (Read-Only)
```

## O que o relatorio mostra

- classes que parecem herdar de `Strategy`;
- campos da classe;
- detalhes de `OnStateChange`;
- mapeamento provavel `campo <= indicador`;
- detalhes de `OnBarUpdate`;
- chamadas de ordem (`EnterLong`, `ExitShort`, `SetStopLoss`, etc.);
- quantidade e offsets de branches condicionais;
- lista de chamadas usadas em `OnBarUpdate`.

## Limites

O mapper nao reconstrói C# perfeito. Ele entrega um mapa estatico para acelerar a leitura.

Para logica ofuscada, use o relatorio como indice:

1. veja os campos em `OnStateChange`;
2. correlacione os campos com chamadas de indicador;
3. abra `OnBarUpdate`;
4. traduza branches e chamadas de ordem para pseudocodigo;
5. mova a regra para `TradingBrain.Core/DecisionEngine.cs`.
