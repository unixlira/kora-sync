# KoraSync — agente de impressão + dashboard nativo (Windows)

Repositório próprio, separado do Kazakora (Laravel) — fala com a API dele
por HTTP, mas não é parte do deploy web. Substitui o agente Node.js
(`print-agent/` no repo do Kazakora) por um app único em C#/.NET (WPF),
self-contained — não precisa de Node, Redis ou PM2 instalados na máquina
de destino.

## Estrutura

- `src/KazakoraAgent.Core` — fila FIFO, retry exponencial, persistência
  SQLite local, cliente da API Laravel. Cross-platform, testado via
  `dotnet test` (não precisa Windows).
- `src/KazakoraAgent.App` — dashboard WPF, bandeja do sistema,
  notificações, impressão real. **Precisa de Windows pra rodar**
  (compila em Linux com `EnableWindowsTargeting`, mas não executa).
- `tests/KazakoraAgent.Core.Tests` — testes do Core.
- `installer/KoraSync.iss` — script do Inno Setup.
- `assets/logo/` — logo original + versão com fundo transparente + `.ico`.

## Rodar em desenvolvimento (Windows, com .NET 8 SDK instalado)

```
dotnet run --project src/KazakoraAgent.App
```

Na primeira execução o app cria `%AppData%\KoraSync\settings.json` com
valores padrão. Edite esse arquivo (ou use o menu Configurações da
bandeja) e preencha:

- `ApiToken` — mesmo valor de `PRINT_AGENT_TOKEN` no `.env` do servidor Laravel.
- `PrinterName` — nome exato da impressora no Windows (Painel de Controle → Dispositivos e Impressoras).

## Rodar os testes do Core

```
dotnet test
```

## Publicar como .exe único (self-contained)

```
dotnet publish src/KazakoraAgent.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Gera `src/KazakoraAgent.App/bin/Release/net8.0-windows/win-x64/publish/KazakoraAgent.App.exe`
— um único arquivo, copiável pra qualquer PC Windows sem instalar nada.

## Gerar o instalador

Precisa do [Inno Setup](https://jrsoftware.org/isinfo.php) instalado (gratuito).
Depois de publicar (passo acima):

1. Abra `installer/KoraSync.iss` no Inno Setup Compiler.
2. Compile (Build → Compile, ou F9).
3. O instalador sai em `installer/dist/KoraSyncSetup.exe`.

Rodar esse `.exe` numa máquina nova instala o app, cria atalho no Menu
Iniciar (e opcionalmente na Área de Trabalho / inicialização com o
Windows, via checkboxes no instalador) — sem precisar repetir o processo
de build.

## O que ainda não foi testado de verdade (precisa de hardware real)

- Impressão física via `WindowsPrinter.cs` (verbo shell `printto`) — se
  não funcionar de primeira, a lib `pdf-to-printer` (usada no agente Node
  anterior, comprovadamente funcional) é o plano B.
- Bandeja do sistema, notificações balloon, atalhos do instalador — nada
  disso pôde ser clicado/visualizado neste ambiente de desenvolvimento
  (sem Windows/WPF runtime disponível).
