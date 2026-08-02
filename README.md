# KoraSync — agente de impressão + dashboard nativo (Windows)

Repositório próprio, separado do Kazakora (Laravel) — fala com a API dele
por HTTP, mas não é parte do deploy web. Substitui o agente Node.js
(`print-agent/` no repo do Kazakora) por um app único em C#/.NET (WPF),
**instalável nativamente no Windows como um programa de verdade**
(instalador com atalho no Menu Iniciar/Área de Trabalho, desinstalador) —
não precisa de Node, Redis ou PM2 instalados na máquina de destino.

## Instalação (numa loja/PC novo)

1. Pegue o instalador já compilado — `installer/dist/KoraSyncSetup.exe`
   (se ele ainda não existir nessa pasta, alguém com Windows + Inno Setup
   precisa gerá-lo primeiro; veja "Gerar o instalador" mais abaixo).
2. Rode o `KoraSyncSetup.exe` e siga o assistente. Ele deixa marcar:
   - Criar atalho na Área de Trabalho
   - Iniciar o KoraSync junto com o Windows
3. Na primeira abertura do app, vá em **bandeja do sistema → Configurações**
   (ou edite direto `%AppData%\KoraSync\settings.json`) e preencha:
   - **ApiToken** — o mesmo valor de `PRINT_AGENT_TOKEN` no `.env` do
     servidor Laravel do Kazakora.
   - **Impressora padrão** — nome exato da impressora já instalada no
     Windows (Painel de Controle → Dispositivos e Impressoras).
4. Pronto — o app fica rodando na bandeja, processando a fila de impressão
   e mostrando o dashboard ao abrir.

Pra desinstalar: Painel de Controle → Programas → KoraSync → Desinstalar
(ou o atalho "Desinstalar KoraSync" que o instalador cria no Menu Iniciar).

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

## Desenvolvimento

Rodar sem instalar (Windows, com .NET 8 SDK):

```
dotnet run --project src/KazakoraAgent.App
```

Rodar os testes do Core (cross-platform, roda em qualquer SO):

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

## O que ainda não foi testado de verdade (precisa de hardware real)

- Impressão física via `WindowsPrinter.cs` (verbo shell `printto`) — se
  não funcionar de primeira, a lib `pdf-to-printer` (usada no agente Node
  anterior, comprovadamente funcional) é o plano B.
- Bandeja do sistema, notificações balloon, atalhos do instalador — nada
  disso pôde ser clicado/visualizado neste ambiente de desenvolvimento
  (sem Windows/WPF runtime disponível).
