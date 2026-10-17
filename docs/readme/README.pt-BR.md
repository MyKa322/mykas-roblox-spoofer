# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer é um aplicativo de terminal para Windows. Na versão 0.3, você pode adicionar contas Roblox em janelas de login separadas, salvar os dados de identidade confirmados e consultar diagnósticos do sistema. A interface tem as abas Accounts, Diagnostics, Adapters e Identity, controles pelo teclado e um logotipo 3D que gira. Para abrir as ferramentas de identidade, pressione 4 e depois Enter ou O.

Na versão 0.3, P em Accounts abre uma experiência após conferir a conta no perfil salvo. O pedido de abertura não confirma que o RobloxPlayer iniciou ou entrou com a conta certa. `identity manage`, ou S em Adapters, abre capturas de antes/depois, comparação, backup e alterações revisadas de GUIDs armazenados e MACs aceitos pelo driver, além de renovação DHCP, limpeza de DNS e limpeza limitada do Roblox. Identificadores de firmware, ambientes simultâneos independentes e roteamento de IP público continuam fora dessas operações. As alterações com privilégios de administrador ainda precisam de teste numa instalação descartável. [Escopo e recuperação](../system-identity.md) e [abertura de experiências](../desktop-launch.md), em inglês.

Este README está em português do Brasil. A interface do aplicativo e os comandos continuam em inglês.

## Requisitos

Para usar a versão compilada, você precisa do Windows x64 e do [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0). Para executar o código-fonte, instale o .NET SDK 10. O login também exige o [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) e acesso à internet.

## Primeiro uso

Abra o PowerShell na pasta principal do projeto e execute o aplicativo a partir do código-fonte:

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

Se a compilação local já estiver pronta, use os comandos abaixo. Mantenha os arquivos auxiliares na mesma pasta que myka.exe:

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## Como adicionar uma conta

1. Na aba Accounts, pressione A. O Roblox será aberto em uma janela com seu próprio perfil WebView2.
2. Digite a senha e os códigos de verificação somente na página do Roblox. Depois de entrar, clique em Verify account.
3. O aplicativo verifica a resposta de `https://users.roblox.com/v1/users/authenticated`. Confira o nome e o ID numérico e clique em Save account.

As contas são identificadas pelo ID numérico do Roblox. Não é possível salvar a mesma conta duas vezes. Um novo login usa um perfil novo; entrar em outra conta ou cancelar não substitui a conta salva. O status `not checked` significa que a validade da sessão não foi verificada naquele momento.

## Comandos

Acrescente estes comandos depois de `.\.artifacts\publish\myka.exe`. Substitua `LOCAL_ACCOUNT_ID` pelo ID local exibido em `accounts list`, não pelo ID numérico do Roblox. Adicionar, entrar novamente e remover contas exige um terminal interativo.

| Comando | O que faz |
| --- | --- |
| `accounts list` | Lista as contas salvas e seus IDs locais. |
| `accounts add` | Abre o login para uma nova conta. |
| `accounts login --account LOCAL_ACCOUNT_ID` | Entra novamente na conta selecionada. |
| `accounts remove --account LOCAL_ACCOUNT_ID` | Pede confirmação para remover a conta e seu perfil local. |
| `accounts cleanup` | Tenta concluir a limpeza dos perfis pendentes. |
| `doctor` | Verifica Windows, .NET, WebView2 e a pasta padrão do Roblox. |
| `identity inspect` | Mostra os adaptadores e os endereços informados pelo Windows. |
| `logo` | Mostra o logotipo. |
| `--help` | Mostra a ajuda dos comandos. |

## Controles

| Teclas | Ação |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | Alternam entre as abas. |
| `↑ / ↓, Home / End, PgUp / PgDn` | Selecionam uma conta ou percorrem listas e relatórios. |
| `A, S / Enter, D / Delete, C` | Em Accounts: adicionar, entrar novamente, remover e limpar perfis. |
| `R` | Recarrega as contas ou atualiza o relatório. |
| `F1 / ?, Esc` | Abre o guia de teclas; Esc fecha o guia. Sem guia ou confirmação aberta, Esc ou Q encerra o aplicativo. |
| `Space` | Pausa ou retoma a rotação do logotipo. |
| `Y` | Confirma a remoção. Qualquer outra tecla mantém a conta. |
| `Ctrl+C` | Cancela e encerra com o código 130. |

As abas precisam de uma janela com pelo menos 50 colunas e 12 linhas. `--plain` usa um menu numerado, sem cores nem animação; `--still` inicia com o logotipo parado. A saída redirecionada, `NO_COLOR` e `TERM=dumb` também usam texto simples.

## Onde os dados ficam

Os dados ficam em `%LOCALAPPDATA%\MyKa\RobloxSpoofer`. Cada conta tem um perfil WebView2 separado, que armazena os dados da sessão do navegador. O arquivo `accounts.json` contém IDs, nomes e horários de verificação, sem senhas ou cookies. O aplicativo não lê cookies nem campos do formulário de login.

A pasta permite acesso ao usuário atual do Windows, ao SYSTEM e aos administradores. Os metadados são substituídos de forma atômica, com uma cópia `.bak`; dados inválidos ou versões desconhecidas do esquema ficam intactos. Remover uma conta exclui o perfil local, mas não encerra sessões Roblox em outros dispositivos.

## Se algo não funcionar

- Se aparecer `WebView2 unavailable`, instale ou repare o WebView2 Runtime. A janela de login permite apenas as páginas Roblox previstas, via HTTPS. Métodos de login de outros provedores que levam a sites externos não são aceitos.
- Se Accounts mostrar `unavailable`, encerre outras operações com contas e pressione R. Não substitua um `accounts.json` danificado por um arquivo vazio.
- Se a limpeza não terminar, feche as janelas de login e execute `accounts cleanup`. Uma verificação incompleta de `doctor` não prova que o Roblox está ausente. Instalações da Store e pastas fora do padrão não são detectadas.

Na última verificação do código, 691 testes passaram. O login na janela real do Roblox, o uso com duas contas e o comportamento em terminais reais ainda precisam de verificação manual. A presença dos arquivos do cliente não confirma compatibilidade nem execução simultânea de clientes.

## Mais informações

- [Arquitetura (inglês)](../architecture.md)
- [Pesquisa e limitações (inglês)](../research.md)
- [Interface do terminal (inglês)](../terminal-ui.md)
- [README completo em inglês, com compilação e testes](../../README.md)
