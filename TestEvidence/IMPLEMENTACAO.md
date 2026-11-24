# TestEvidence — Documento de implementação

Ferramenta desktop (Windows Forms, .NET 8) para **gerar evidência de teste sem sair do fluxo de teste**: o testador inicia uma sessão informando a tarefa, uma barra flutuante fica sempre por cima das outras janelas, e a cada passo ele aperta **Evidenciar** (botão ou atalho global), seleciona um pedaço da tela e escreve uma legenda opcional. No fim, a ferramenta monta o documento Word (`.docx`) com cabeçalho, etapas (Antes / Execução / Resultado), prints e legendas.

Hoje o processo é manual: tirar print, colar no Word, escrever "antes de fazer X", "depois de fazer X", repetir. O objetivo é eliminar o colar-e-formatar e deixar só o testar.

Este projeto nasce como mais uma ferramenta do **apps-cm**, igual ao `TestCoverageUI` e ao `git-submodule-sync`: repositório próprio, adicionado ao apps-cm como **submódulo**, hospedado numa aba do `AppsCm.exe` e compilando também standalone.

---

## 1. Escopo

### MVP (este documento)

1. Criar sessão de evidência com cabeçalho (tarefa, título, requisito, cenário, ambiente, testador).
2. Barra flutuante `TopMost`, arrastável, com seletor de etapa e botões Evidenciar / Pausar / Finalizar.
3. Atalho global para evidenciar (padrão `Ctrl+Shift+E`) e para repetir a última região (padrão `Ctrl+Shift+R`).
4. Captura por **seleção de região** (estilo Ferramenta de Captura), **tela inteira** e **repetir última região**, funcionando com vários monitores e escalas de DPI diferentes.
5. Legenda rápida após cada captura (`Enter` confirma, `Esc` deixa sem legenda).
6. Sessão persistida em disco a cada captura (crash-safe) e possível de retomar.
7. Tela de revisão: reordenar, mudar etapa, editar legenda, excluir, marcar o resultado (Aprovado / Reprovado / Com ressalvas) e observações.
8. Gerar `.docx` com layout padrão definido em código.
9. Aba no apps-cm com o histórico de sessões (abrir, retomar, regerar documento, abrir pasta, excluir).

### Fora do MVP (fases futuras, ver seção 11)

- Template `.docx` customizado da empresa com placeholders.
- Anotações no print (seta, retângulo, texto, borrar dado sensível).
- Exportar PDF.
- Modo automático (um print a cada clique).
- Integração com Jira (puxar título/requisito pelo ID da tarefa).

---

## 2. Estrutura da solução

Seguir **exatamente** o padrão dos outros submódulos (`UI → Services → Models`, sem container de DI, persistência no próprio model):

```
TestEvidence/
  Directory.Build.props
  TestEvidence.sln
  LICENSE
  readme.md
  CLAUDE.md                      <- orientações para agentes (seguir o modelo do TestCoverageUI/CLAUDE.md)
  IMPLEMENTACAO.md               <- este arquivo
  TestEvidence.Models/
    TestEvidence.Models.csproj   <- net8.0, sem dependências
    CaminhoConfig.cs
    ConfiguracaoApp.cs
    SessaoEvidencia.cs
    Evidencia.cs
    Enums.cs
  TestEvidence.Services/
    TestEvidence.Services.csproj <- net8.0-windows, UseWindowsForms (precisa de System.Drawing/Screen)
    Nativo/
      User32.cs                  <- P/Invoke: RegisterHotKey, UnregisterHotKey, SetThreadDpiAwarenessContext
    CapturaService.cs
    SessaoRepository.cs
    DocumentoWordService.cs
    AtalhoGlobal.cs
  TestEvidence.UI/
    TestEvidence.UI.csproj       <- WinExe, net8.0-windows, UseWindowsForms, AssemblyName TestEvidence
    Program.cs
    favicon.ico
    MainForm.cs (+ Designer)     <- histórico de sessões; é o form hospedado na aba do shell
    NovaSessaoForm.cs (+ Designer)
    BarraFlutuanteForm.cs (+ Designer)
    SelecaoRegiaoForm.cs         <- overlay de seleção (desenhado em código, sem Designer)
    LegendaForm.cs (+ Designer)
    RevisaoForm.cs (+ Designer)
    ConfigForm.cs (+ Designer)
    ControladorSessao.cs         <- orquestra barra flutuante + captura + persistência
```

### `Directory.Build.props` (copiar do git-submodule-sync)

```xml
<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <NeutralLanguage>pt-BR</NeutralLanguage>
  </PropertyGroup>
</Project>
```

### Dependências NuGet

| Projeto | Pacote | Uso |
|---|---|---|
| `TestEvidence.Services` | `DocumentFormat.OpenXml` (3.x, licença MIT) | Gerar o `.docx` |

Nenhuma outra dependência. **Não** usar Interop do Office (`Microsoft.Office.Interop.Word`): exige Word instalado e é frágil.

### `TestEvidence.UI.csproj` — ícone solto na saída

Igual ao `GitSubmoduleSync.UI.csproj`, para o shell conseguir carregar o ícone em runtime:

```xml
<ItemGroup>
  <None Include="favicon.ico" Link="Icone\TestEvidence.ico" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

---

## 3. Modelo de dados (`TestEvidence.Models`)

### Enums

```csharp
public enum ResultadoTeste { Pendente, Aprovado, Reprovado, ComRessalvas }
public enum StatusSessao { EmAndamento, Pausada, Finalizada }
public enum TipoCaptura { Regiao, TelaInteira, RepetirRegiao }
```

### `SessaoEvidencia`

```csharp
public sealed class SessaoEvidencia
{
  public string Id { get; set; } = "";              // = nome da pasta da sessão
  public string Tarefa { get; set; } = "";          // ex.: "PROJ-1234" (obrigatório)
  public string Titulo { get; set; } = "";          // ex.: "Cadastro de cliente com CPF inválido"
  public string Requisito { get; set; } = "";       // texto livre, multilinha
  public string Cenario { get; set; } = "";         // texto livre, multilinha (passos/pré-condições)
  public string Ambiente { get; set; } = "";        // ex.: "Homologação", "Dev", URL...
  public string Testador { get; set; } = "";        // default: Environment.UserName
  public DateTime Inicio { get; set; }
  public DateTime? Fim { get; set; }
  public StatusSessao Status { get; set; }
  public ResultadoTeste Resultado { get; set; } = ResultadoTeste.Pendente;
  public string Observacoes { get; set; } = "";
  public List<string> Etapas { get; set; } = new() { "Antes", "Execução", "Resultado" };
  public string EtapaAtual { get; set; } = "Antes";
  public List<Evidencia> Evidencias { get; set; } = new();
  public Rectangle? UltimaRegiao { get; set; }      // em pixels físicos, coordenadas da tela virtual
  public string? UltimoDocumentoGerado { get; set; }
}
```

> `Rectangle` é de `System.Drawing`; se preferir manter `Models` sem dependência de Windows, criar um `record RegiaoTela(int X, int Y, int Largura, int Altura)`. **Recomendado: o record**, para o Models ficar `net8.0` puro.

### `Evidencia`

```csharp
public sealed class Evidencia
{
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public int Ordem { get; set; }
  public string Etapa { get; set; } = "";
  public string Legenda { get; set; } = "";
  public string ArquivoImagem { get; set; } = "";   // relativo à pasta da sessão: "img\\001.png"
  public DateTime CapturadoEm { get; set; }
  public TipoCaptura Tipo { get; set; }
}
```

### `ConfiguracaoApp` (configs.json)

```csharp
public sealed class ConfiguracaoApp
{
  public string AtalhoEvidenciar { get; set; } = "Ctrl+Shift+E";
  public string AtalhoRepetirRegiao { get; set; } = "Ctrl+Shift+R";
  public bool PedirLegendaAposCaptura { get; set; } = true;
  public string PastaSaidaDocumentos { get; set; } = "";   // vazio = Documentos\Evidências
  public string UltimoTestador { get; set; } = "";
  public string UltimoAmbiente { get; set; } = "";
  public List<string> EtapasPadrao { get; set; } = new() { "Antes", "Execução", "Resultado" };
  public Point? PosicaoBarra { get; set; }                 // última posição da barra flutuante
}
```

Carregar/salvar com `System.Text.Json` (`WriteIndented = true`), no mesmo estilo do `ProfilesConfig` do TestCoverageUI: método estático `Carregar()` que cria com defaults se não existir, e `Salvar()`.

### `CaminhoConfig`

Seguir o padrão de `GitSubmoduleSync.Models/CaminhoConfig.cs` (sem a migração de legado, que aqui não existe):

```csharp
public static class CaminhoConfig
{
  private const string AppId = "test-evidence";

  public static string Pasta => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apps-cm", AppId);

  public static string Arquivo => Path.Combine(Pasta, "configs.json");
  public static string PastaSessoes => Path.Combine(Pasta, "sessoes");
}
```

O `AppId` **tem que ser** `test-evidence`, igual ao `Id` do módulo no shell (ver seção 9).

---

## 4. Persistência da sessão (`SessaoRepository`)

Cada sessão é uma pasta:

```
%LOCALAPPDATA%\apps-cm\test-evidence\sessoes\
  20261123-143012-PROJ-1234\
    sessao.json
    img\
      001.png
      002.png
      ...
```

- `Id` da sessão = `yyyyMMdd-HHmmss-{Tarefa sanitizada}` (remover caracteres inválidos de nome de arquivo com `Path.GetInvalidFileNameChars()`, limitar a ~40 caracteres).
- Imagens salvas em **PNG**, numeradas com 3 dígitos pela ordem de captura. O número do arquivo **não** muda quando o usuário reordena: a ordem vale pelo campo `Ordem`.
- `sessao.json` é regravado **a cada captura e a cada edição**. Gravar em `sessao.json.tmp` e trocar com `File.Replace` (ou `File.Move(..., overwrite: true)`) para não corromper se o app cair no meio da escrita.
- Excluir uma evidência na revisão apaga o PNG correspondente.

API sugerida:

```csharp
public sealed class SessaoRepository
{
  public SessaoEvidencia Criar(SessaoEvidencia dadosIniciais);   // cria pasta, define Id/Inicio/Status
  public void Salvar(SessaoEvidencia sessao);
  public SessaoEvidencia? Carregar(string id);
  public IReadOnlyList<SessaoEvidencia> Listar();                // ordenado por Inicio desc; ignora pastas com json inválido
  public string PastaDa(SessaoEvidencia sessao);
  public string AdicionarImagem(SessaoEvidencia sessao, Bitmap imagem); // salva PNG, devolve caminho relativo
  public void ExcluirEvidencia(SessaoEvidencia sessao, Evidencia evidencia);
  public void ExcluirSessao(string id);                          // mandar para a lixeira (Microsoft.VisualBasic.FileIO) se possível
}
```

**Retomar sessão:** ao abrir o app, se existir sessão com `Status != Finalizada`, o `MainForm` mostra destaque "Você tem uma evidência em andamento: PROJ-1234 — Retomar?".

---

## 5. Captura de tela (`CapturaService` + `SelecaoRegiaoForm`)

Esta é a parte com mais armadilhas. Ler com atenção.

### 5.1 DPI

O `AppsCm.Shell` usa `ApplicationConfiguration.Initialize()` sem `ApplicationHighDpiMode` definido, ou seja, o processo roda **SystemAware**. Nesse modo, em notebook com escala 125%/150% e monitor externo a 100%, as coordenadas do Windows Forms **não batem** com os pixels físicos, e o print sai cortado/deslocado.

**Não** alterar o modo de DPI do shell (afetaria as outras ferramentas). Em vez disso:

- Toda a captura e o overlay de seleção rodam com **contexto de DPI por thread** `PER_MONITOR_AWARE_V2`:

```csharp
internal static class User32
{
  public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

  [DllImport("user32.dll")]
  public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
}

// uso
var anterior = User32.SetThreadDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
try
{
  // criar e mostrar o SelecaoRegiaoForm, ler SystemInformation.VirtualScreen, CopyFromScreen...
}
finally
{
  User32.SetThreadDpiAwarenessContext(anterior);
}
```

  O form do overlay precisa ser **criado** (handle criado) enquanto o contexto está ativo, porque o awareness da janela é fixado na criação.

- Guardar `UltimaRegiao` sempre em **pixels físicos** da tela virtual.
- Testar obrigatoriamente: monitor único 100%; notebook 150% + monitor externo 100%; monitor secundário à esquerda do principal (coordenadas X negativas).

### 5.2 Fluxo "congelar e recortar"

Não capturar a tela **depois** que o usuário seleciona (o overlay sairia no print, e menus/tooltips abertos fecham ao perder o foco). Fazer:

1. Esconder a barra flutuante (`Opacity = 0` ou `Hide()`) e esperar ~150 ms (`await Task.Delay(150)`) para o DWM redesenhar sem ela.
2. Capturar a **tela virtual inteira** (`SystemInformation.VirtualScreen`, todos os monitores) num `Bitmap` com `Graphics.CopyFromScreen`.
3. Abrir o `SelecaoRegiaoForm` cobrindo a tela virtual inteira, mostrando esse bitmap congelado com uma camada escura semitransparente por cima.
4. O usuário arrasta o retângulo; dentro da seleção a imagem aparece sem a camada escura. Mostrar a dimensão em pixels perto do cursor (ex.: `824 × 410`).
5. Ao soltar o mouse: recortar a região **do bitmap congelado** (não da tela de novo) e devolver.
6. Mostrar a barra flutuante de novo.

Teclas no overlay:

| Tecla | Ação |
|---|---|
| `Esc` / botão direito | Cancela a captura |
| `Enter` sem seleção | Usa a tela inteira do monitor onde está o cursor |
| `Espaço` | Usa a última região (se houver) |

Requisitos do `SelecaoRegiaoForm`:
- `FormBorderStyle = None`, `StartPosition = Manual`, `Bounds = VirtualScreen`, `TopMost = true`, `ShowInTaskbar = false`, `DoubleBuffered = true`, `Cursor = Cursors.Cross`.
- Desenhar tudo no `OnPaint` (sem controles filhos).
- Seleção menor que 5×5 px é tratada como clique sem seleção (ignorar, não gerar print vazio).

### 5.3 Outros modos

- **Tela inteira:** monitor onde está o cursor (`Screen.FromPoint(Cursor.Position)`), não a tela virtual inteira.
- **Repetir última região:** captura direto `UltimaRegiao` sem overlay (ainda escondendo a barra antes). Se não houver última região, cai no modo região.

### 5.4 API

```csharp
public sealed class CapturaService
{
  // Esconder/mostrar a barra é responsabilidade de quem chama (ControladorSessao)
  public Bitmap CapturarTelaVirtual();
  public Bitmap CapturarRegiao(RegiaoTela regiao);
  public Bitmap CapturarMonitorDoCursor(out RegiaoTela regiao);
}
```

Lembrar de dar `Dispose` em todos os `Bitmap`/`Graphics`.

---

## 6. Atalhos globais (`AtalhoGlobal`)

- Usar `RegisterHotKey`/`UnregisterHotKey` (user32) com o handle da `BarraFlutuanteForm`, tratando `WM_HOTKEY` (`0x0312`) no `WndProc` dela.
- Usar `MOD_NOREPEAT` (`0x4000`) para segurar a tecla não disparar várias capturas.
- Registrar quando a barra abre e desregistrar no `OnFormClosed`/`OnHandleDestroyed`.
- Se `RegisterHotKey` falhar (atalho já usado por outro programa): **não** bloquear; mostrar um aviso discreto na barra ("Atalho Ctrl+Shift+E em uso por outro app — altere em Configurações") e seguir funcionando pelo botão.
- Parser simples da string do atalho (`"Ctrl+Shift+E"` → modificadores + `Keys`) com validação no `ConfigForm` (precisa de pelo menos um modificador).

---

## 7. Interface (`TestEvidence.UI`)

Textos de UI em pt-BR. Indentação de 2 espaços. Lógica no `.cs`, não no `.Designer.cs`.

### 7.1 `MainForm`: histórico (é o que aparece na aba do shell)

- Botão principal **"Nova evidência"**.
- Faixa de destaque quando houver sessão não finalizada: "Evidência em andamento: {Tarefa} — {Título}" + botão **Retomar**.
- `ListView` (modo Details) com as sessões: Tarefa, Título, Início, Qtde. prints, Resultado, Status.
- Ações na seleção (botões e menu de contexto): **Abrir revisão**, **Retomar**, **Gerar documento**, **Abrir documento**, **Abrir pasta**, **Excluir**.
- Botão **Configurações** (abre o `ConfigForm`).
- Expor `public bool EmSessaoAtiva` para o shell perguntar antes de fechar a aba (igual ao `EmExecucao` do GitSubmoduleSync).

> **Importante:** o shell **reparenteia** o `MainForm` dentro de uma aba. Por isso a barra flutuante **não pode** ser filha dele: ela é um `Form` top-level separado, criado com `new BarraFlutuanteForm(...).Show()` **sem owner** (com owner, ela minimizaria junto com o shell).

### 7.2 `NovaSessaoForm`

Campos: Tarefa* (obrigatório), Título, Requisito (multilinha), Cenário/Pré-condições (multilinha), Ambiente (combo editável com histórico, default `UltimoAmbiente`), Testador (default `UltimoTestador` ou `Environment.UserName`), Etapas (lista editável, default `EtapasPadrao`).

Botão **Iniciar**: cria a sessão no repositório, salva `UltimoTestador`/`UltimoAmbiente`, minimiza o shell (opcional, configurável; default sim) e abre a barra flutuante.

### 7.3 `BarraFlutuanteForm`

Janela pequena (~ 420 × 44 px), sempre visível:

```
[≡ PROJ-1234] [Etapa: Antes ▼] [📸 Evidenciar] [▭ Repetir] [🖥 Tela] | 3 prints | [⏸] [✔ Finalizar]
```

- `FormBorderStyle = None`, `TopMost = true`, `ShowInTaskbar = true` (para o usuário achar pelo Alt+Tab), arrastável clicando em qualquer área vazia ou no "≡" (via `WM_NCLBUTTONDOWN`/`HTCAPTION` ou tratando MouseDown/Move).
- Posição inicial: `PosicaoBarra` salva; se ela estiver fora de todas as telas atuais (monitor desconectado), usar o canto superior central do monitor principal.
- **Seletor de etapa:** `ComboBox` com as etapas da sessão + opção "Nova etapa…". Trocar a etapa atualiza `EtapaAtual` e salva a sessão. Atalho: `Ctrl+Shift+1..9` (opcional no MVP).
- Contador de prints ao vivo.
- **Pausar:** desregistra os atalhos e deixa a barra semitransparente; clicar de novo retoma.
- **Finalizar:** fecha a barra e abre o `RevisaoForm`.
- Fechar a barra pelo `Alt+F4` = pausar (a sessão continua em disco, retomável pelo histórico). Nunca perder dados.
- Feedback de captura: um "flash" rápido na barra (ex.: fundo verde por 300 ms) + o contador incrementa. Sem som.

### 7.4 `LegendaForm`

Abre logo depois da captura (se `PedirLegendaAposCaptura`), **perto da barra flutuante** e não no centro da tela:

- Miniatura do print à esquerda, `TextBox` de legenda à direita com foco automático.
- Mostra a etapa atual (permite trocar ali mesmo).
- `Enter` = salvar; `Shift+Enter` = quebra de linha; `Esc` = salvar sem legenda; botão **Descartar** = joga o print fora.
- `TopMost`, sem aparecer na barra de tarefas.

### 7.5 `RevisaoForm`

- Cabeçalho editável (mesmos campos da `NovaSessaoForm`).
- Lista das evidências agrupadas por etapa, cada item com miniatura, etapa (combo), legenda (editável) e horário.
- Reordenar: botões ↑ ↓ (drag-and-drop opcional).
- Excluir item (com confirmação).
- Clique duplo na miniatura abre a imagem no visualizador padrão do Windows (`Process.Start` com `UseShellExecute = true`).
- **Resultado:** radio Aprovado / Reprovado / Com ressalvas + Observações (multilinha).
- Botões: **Voltar a evidenciar** (reabre a barra), **Salvar**, **Gerar documento**.
- **Gerar documento:** `SaveFileDialog` com nome sugerido `Evidencia_{Tarefa}_{yyyyMMdd}.docx` na `PastaSaidaDocumentos`; ao terminar, marca `Status = Finalizada`, `Fim = agora`, grava `UltimoDocumentoGerado` e pergunta "Abrir o documento?".

### 7.6 `ConfigForm`

Atalhos (com validação e teste de registro), pedir legenda após captura, minimizar o shell ao iniciar, pasta de saída padrão, etapas padrão.

### 7.7 `ControladorSessao`

Classe sem UI própria que segura a sessão ativa e liga as peças, para a lógica não ficar espalhada pelos forms:

```csharp
public sealed class ControladorSessao
{
  public SessaoEvidencia Sessao { get; }
  public event EventHandler? SessaoAlterada;          // a barra e o MainForm atualizam contadores

  public Task EvidenciarAsync(TipoCaptura tipo);      // esconde barra → captura → mostra barra → legenda → salva
  public void TrocarEtapa(string etapa);
  public void Pausar();
  public void Retomar();
  public void Finalizar();                            // fecha barra, abre revisão
}
```

Garantir que só exista **uma** sessão ativa por vez (singleton simples no processo). Ignorar um novo `Evidenciar` enquanto um anterior ainda está em andamento.

---

## 8. Geração do documento (`DocumentoWordService`)

Usar `DocumentFormat.OpenXml`. Página **A4 retrato**, margens de 2 cm, fonte Calibri 11 (ou a padrão do Word).

### Layout padrão

```
EVIDÊNCIA DE TESTE                                   (título, 16pt, negrito)

┌──────────────────┬──────────────────────────────────────┐
│ Tarefa           │ PROJ-1234                            │
│ Título           │ Cadastro de cliente com CPF inválido │
│ Requisito        │ ...                                  │
│ Cenário          │ ...                                  │
│ Ambiente         │ Homologação                          │
│ Testador         │ fulano                               │
│ Data/Hora        │ 23/11/2026 14:30 – 15:02             │
│ Resultado        │ APROVADO   (verde / vermelho / laranja) │
└──────────────────┴──────────────────────────────────────┘

1. Antes                                             (Heading 1)

  Evidência 1 — 14:31:05                             (negrito, 10pt)
  Tela inicial do cadastro                           (legenda)
  [imagem]

  Evidência 2 — 14:31:40
  ...

2. Execução                                          (Heading 1)
  ...

3. Resultado
  ...

Observações                                          (Heading 1, só se houver)
  ...
```

Regras:

- Etapas na ordem da lista `Sessao.Etapas`; etapas **sem** evidência não aparecem. Evidências dentro de cada etapa pela `Ordem`.
- Numeração de evidência **contínua** no documento todo (1, 2, 3...), não reinicia por etapa.
- Usar os estilos `Heading1`/`Title` do Word (criar a `StyleDefinitionsPart`) para o sumário/navegação do Word funcionar.
- **Imagens:**
  - Largura máxima = largura útil da página (A4 com 2 cm de margem ≈ **17 cm**). Imagem menor que isso fica no **tamanho original** (não ampliar print pequeno, fica borrado).
  - Converter pixels → EMU considerando 96 DPI: `emu = px * 9525`. 1 cm = 360000 EMU.
  - Altura proporcional. Se a altura passar de ~22 cm, reduzir proporcionalmente para caber numa página.
  - Borda fina cinza na imagem (`a:ln` no `pic:spPr`) para destacar prints de telas brancas.
  - Cada imagem precisa de `DocProperties.Id` **único** no documento (usar contador incremental). Id repetido gera documento que o Word diz estar corrompido.
  - Manter título + legenda + imagem juntos (`KeepNext` nos parágrafos de título e legenda).
- Rodapé: "Gerado por TestEvidence {versão} em {data/hora}" + número da página (campo `PAGE`).
- Se o arquivo de destino estiver aberto no Word (`IOException`), mostrar mensagem clara: "Feche o documento no Word e tente de novo".

Isolar o layout numa classe (`LayoutPadraoDocumento` ou métodos privados bem separados) para que a fase do template customizado (seção 11) só troque essa parte.

API:

```csharp
public sealed class DocumentoWordService
{
  public void Gerar(SessaoEvidencia sessao, string pastaSessao, string caminhoDocx);
}
```

---

## 9. Integração com o apps-cm

### 9.1 Repositório e submódulo

1. Criar o repositório `TestEvidence` no GitHub (mesma conta dos outros) e mover o conteúdo desta pasta para ele.
2. No apps-cm, **substituir** esta pasta pelo submódulo:
   ```powershell
   git submodule add https://github.com/christiandmelo/TestEvidence TestEvidence
   ```
3. Adicionar os 3 projetos no `AppsCm.sln` (mesmo padrão de `TestCoverageUI.*` e `GitSubmoduleSync.*`).

### 9.2 Módulo no shell

Criar `src/AppsCm.Shell/Modulos/ModuloTestEvidence.cs`:

```csharp
using AppsCm.Shell.Contratos;

namespace AppsCm.Shell.Modulos;

internal sealed class ModuloTestEvidence : IAppModule
{
  public string Id => "test-evidence";
  public string NomeExibicao => "Evidência de Teste";
  public string? Versao => typeof(TestEvidence.UI.MainForm).Assembly.GetName().Version?.ToString();
  public Icon? Icone => IconeDoArquivo.Carregar("Icone", "TestEvidence.ico");

  public Control CriarView() => new TestEvidence.UI.MainForm();

  public bool PodeFechar(Control view)
  {
    if (view is not TestEvidence.UI.MainForm form || !form.EmSessaoAtiva)
      return true;

    var resposta = MessageBox.Show(
      "Há uma evidência em andamento. Ela fica salva e pode ser retomada depois. Fechar a aba mesmo assim?",
      "Evidência de Teste", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
    return resposta == DialogResult.Yes;
  }
}
```

Ao fechar a aba com sessão ativa, o `MainForm` precisa **fechar a barra flutuante** (no `OnFormClosed` dele) e salvar a sessão como `Pausada`. Senão a barra fica órfã na tela.

### 9.3 Alterações em arquivos do apps-cm

| Arquivo | Alteração |
|---|---|
| `src/AppsCm.Shell/ShellForm.cs` | Adicionar `new ModuloTestEvidence()` no array `Modulos` |
| `src/AppsCm.Shell/AppsCm.Shell.csproj` | `ProjectReference` para `..\..\TestEvidence\TestEvidence.UI\TestEvidence.UI.csproj` e incluir `$(PublishDir)TestEvidence.exe` no `<Delete>` do target `RemoverAppHostsDosModulos` |
| `AppsCm.sln` | Os 3 projetos novos |
| `.gitmodules` | Gerado pelo `git submodule add` |
| `README.md` | Citar o `TestEvidence` na estrutura e nos comandos de "rodar standalone" |

`Release.ps1` e o instalador **não** precisam mudar: o publish do shell já leva as DLLs dos módulos referenciados.

### 9.4 Standalone

`Program.cs` do `TestEvidence.UI` roda o `MainForm` normalmente (`ApplicationConfiguration.Initialize(); Application.Run(new MainForm());`). Toda inicialização necessária (carregar config, criar pastas) tem que acontecer no `MainForm`, **não** no `Program.Main`, porque sob o shell o `Program.Main` nunca roda (mesma regra do TestCoverageUI).

---

## 10. Convenções

- **Indentação de 2 espaços**; `Nullable`/`ImplicitUsings`/`LangVersion=latest` via `Directory.Build.props`.
- Código, comentários, textos de UI, mensagens de erro e commits em **pt-BR**.
- Services não mostram `MessageBox`: lançam exceção com mensagem em pt-BR ou retornam resultado; quem mostra é a UI.
- Erros de I/O (disco cheio, arquivo em uso, pasta sem permissão) nunca podem derrubar o app nem perder a sessão: capturar, avisar e seguir.
- Sem projetos de teste no MVP (mesmo padrão dos outros submódulos). Se for adicionar, priorizar `DocumentoWordService` (gerar e reabrir o `.docx` com OpenXml para validar) e o parser de atalhos.

---

## 11. Fases futuras

1. **Template da empresa:** o usuário escolhe um `.docx` modelo com placeholders (`{{Tarefa}}`, `{{Titulo}}`, `{{Requisito}}`, `{{Cenario}}`, `{{Ambiente}}`, `{{Testador}}`, `{{Data}}`, `{{Resultado}}`, `{{Observacoes}}`) e um parágrafo marcador `{{Evidencias}}`, onde entram as etapas e os prints. Atenção: o Word costuma quebrar um placeholder em vários `Run`; normalizar os runs do parágrafo antes de substituir.
2. **Anotações** no `LegendaForm`/`RevisaoForm`: retângulo vermelho, seta, texto, **borrar/tarjar** região (dados sensíveis como CPF). Salvar a imagem anotada separada da original (`001.png` e `001.anotada.png`).
3. **PDF:** gerar o `.docx` e converter (avaliar biblioteca; não depender do Word instalado).
4. **Modo automático:** hook global de mouse (`SetWindowsHookEx` `WH_MOUSE_LL`) gerando um print da janela ativa a cada clique, com um destaque no ponto clicado.
5. **Jira:** buscar título/descrição pelo ID da tarefa e, opcionalmente, anexar o `.docx` na issue.
6. **Copiar para a área de transferência** cada print (para quem quiser colar em chat na hora).

---

## 12. Critérios de aceite do MVP

- [ ] Abrir pelo menu **Apps → Evidência de Teste** no `AppsCm.exe` e também standalone (`dotnet run --project TestEvidence.UI`).
- [ ] Criar sessão, a barra aparece por cima de qualquer janela (inclusive navegador em tela cheia não exclusiva).
- [ ] `Ctrl+Shift+E` funciona com outro app em foco; a barra não aparece no print.
- [ ] Seleção de região correta (sem deslocamento) em: monitor único 100%; notebook 150% + monitor externo 100%; monitor secundário à esquerda do principal.
- [ ] Menu suspenso/tooltip aberto no app testado aparece no print (efeito do "congelar e recortar").
- [ ] Matar o processo no meio da sessão (Gerenciador de Tarefas) e ao reabrir conseguir retomar com todos os prints já tirados.
- [ ] Reordenar, trocar etapa, editar legenda e excluir na revisão refletem no documento.
- [ ] `.docx` abre no Word sem aviso de "conteúdo ilegível", com cabeçalho, etapas como títulos, prints legíveis (print pequeno não é ampliado) e resultado colorido.
- [ ] Fechar a aba com sessão ativa pergunta, fecha a barra flutuante e a sessão fica como Pausada.
- [ ] Atalho já em uso por outro app não quebra nada (aviso e o botão continua funcionando).

---

## 13. Ordem sugerida de implementação

1. Estrutura da solução, `Directory.Build.props`, Models, `CaminhoConfig`, `SessaoRepository`.
2. `CapturaService` + `SelecaoRegiaoForm` com o tratamento de DPI. Testar isolado num form de teste antes de seguir. **É o maior risco técnico.**
3. `BarraFlutuanteForm` + `AtalhoGlobal` + `ControladorSessao` + `LegendaForm`.
4. `NovaSessaoForm` e `MainForm` (histórico, retomar).
5. `DocumentoWordService`.
6. `RevisaoForm`.
7. `ConfigForm`.
8. Integração com o apps-cm (seção 9) e teste dos critérios de aceite.
