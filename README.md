# Ace Combat Zero — instalador da tradução PT-BR

**Tradução por Carreta.** Código-fonte do instalador offline de Ace Combat Zero: The Belkan War, versão 010. Publicado para permitir a inspeção e a compilação do executável enviado ao Nexus Mods.

Os seis arquivos de implementação em `src/`, o manifesto e as imagens são os mesmos usados no último instalador, **`AC0 PT-BR.exe`**, com os créditos no canto inferior esquerdo. A identidade do arquivo distribuído e os hashes dos fontes estão em [`provenance/release.json`](provenance/release.json).

```text
Arquivo:  AC0 PT-BR.exe
Tamanho:  530076672 bytes
SHA-256:  3b1f91be236963001812f1e572c10a091395549d4812b47e97625901ef3f6715
```

This repository contains the C# source of the offline Windows installer submitted to Nexus Mods. English build and review instructions are in [BUILDING.md](BUILDING.md) and [REVIEW.md](REVIEW.md).

## O que o instalador faz

- Permite selecionar a pasta `ACZero`, com `acz.exe`, `a.arc` e `d.arc`.
- Verifica os arquivos ARC por SHA-256, o espaço livre e se o jogo está fechado.
- Cria um backup dos dois ARC antes da substituição e registra a operação para recuperação em caso de falha.
- Instala os ARC traduzidos que já estão dentro do executável. Não modifica `acz.exe` e não verifica sua versão por hash.
- Exibe o resultado na janela principal e orienta a selecionar Inglês (US) no jogo.

O código não acessa a rede, não baixa programas, não cria serviços e não modifica o Registro do Windows. A janela de escolha de pasta usa a API do Windows. Veja a descrição do comportamento em [REVIEW.md](REVIEW.md).

## Compilar

Requisitos: Windows 64 bits, **Windows PowerShell 5.1** e o compilador do .NET Framework 4, normalmente presente em `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Não precisa instalar NuGet ou outra biblioteca.

O repositório não inclui o jogo, os ARC ou o instalador de 505,5 MiB. Quem estiver revisando o arquivo distribuído pode fornecer uma cópia dele para recuperar somente os dois recursos de dados embutidos:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -InstallerExe "C:\caminho\AC0 PT-BR.exe"
```

O script confere primeiro o SHA-256 do arquivo informado. Ele lê os recursos de dados pela API de assemblies do .NET sem chamar o ponto de entrada do instalador original. Depois, compila os fontes deste repositório e verifica os pacotes do novo executável. Saída: `dist\AC0 PT-BR.exe`.

Como alternativa, forneça os dois ARC traduzidos da versão 010:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -PayloadDirectory "C:\caminho\pacotes-v010"
```

Os hashes obrigatórios estão em [`package/manifest.json`](package/manifest.json). Usar os ARC originais do jogo nesse comando não funciona: o build requer os ARC já traduzidos.

Consulte [BUILDING.md](BUILDING.md) para os testes e detalhes da reconstrução. A nova compilação pode ter um SHA-256 diferente por causa dos identificadores e metadados gerados pelo compilador; o hash acima identifica o arquivo distribuído, não qualquer recompilação.

## Estrutura

| Caminho | Conteúdo |
| --- | --- |
| `src/InstallerEngine.cs` | Validação, hashes, backup, instalação e recuperação |
| `src/InstallerForm.cs` | Janela principal e créditos |
| `src/InstallerDialogs.cs` | Campo com exemplo e diálogo de pasta inválida |
| `src/FolderPicker.cs` | Seletor de pasta do Windows |
| `src/BufferedControls.cs` | Controles com buffering |
| `src/Program.cs` | Inicialização, verificação de pacote e prévias |
| `assets/` | Ícone, título e fundo usados no instalador |
| `tests/` | Testes com dados artificiais, sem exigir o jogo |
| `package/manifest.json` | Hashes e estados ARC aceitos pela versão 010 |

## Créditos e direitos

Tradução e distribuição: **Carreta**. Veja [NOTICE.md](NOTICE.md). Este repositório publica o código do instalador; não contém o código-fonte de Ace Combat Zero nem os arquivos de dados do jogo.
