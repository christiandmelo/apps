# Tema visual do apps-cm

Padroniza o visual do shell e de todos os apps: cores, fontes, botões, listas e painéis. **Fazer depois da [migração para monorepo](MIGRACAO-MONOREPO.md)**, porque o tema vira um projeto comum referenciado por todos.

## Situação atual

O tema foi prototipado e aprovado na tela principal do TestEvidence:
- [TestEvidence/TestEvidence.UI/Visual/Tema.cs](../TestEvidence/TestEvidence.UI/Visual/Tema.cs): paleta, fontes, fábricas de botão, estilo de lista e os painéis `Cartao`, `FaixaComBorda` e `Aviso`.
- [TestEvidence/TestEvidence.UI/MainForm.cs](../TestEvidence/TestEvidence.UI/MainForm.cs): primeira tela com o tema (layout novo, comportamento igual).

O protótipo já está commitado no TestEvidence ("Protótipo do tema visual na tela principal") e entra no apps-cm junto com o histórico na migração.

## Como executar (pessoa ou IA)
- **Uma tela por vez**, de preferência uma conversa por tela. Pedido modelo: "siga o passo 3 do docs/TEMA-VISUAL.md só para a `NovaSessaoForm`; use a `TestEvidence/TestEvidence.UI/MainForm.cs` como referência de estilo; não mude a lógica".
- A referência de como fica uma tela pronta é o `MainForm` do TestEvidence: copiar a estrutura (cabeçalho `FaixaComBorda`, corpo com padding 20/16, conteúdo em `Cartao`, rodapé `FaixaComBorda` com os botões) em vez de inventar layout novo.
- Antes de mexer numa tela, listar os eventos, atalhos, `AcceptButton`/`CancelButton` e a ordem de tab dela, e conferir depois que todos continuam iguais.
- Compilar depois de cada tela. O feed NuGet interno da TOTVS responde 401 (erro `NU1301`) no restore, então:
  ```powershell
  dotnet restore <arquivo.sln> --ignore-failed-sources
  dotnet build <arquivo.sln> --no-restore
  ```
- Abrir o app e olhar a tela antes de commitar. Um commit por tela ("Tema visual: NovaSessaoForm"), para poder descartar só a que der errado.
- Nunca fazer `git push`, e mensagens de commit sem rodapé de atribuição (`Co-Authored-By`, "Generated with...").
- Ordem sugerida, da mais simples para a mais delicada: passos 1 e 2; telas com `.Designer.cs` (TestCoverageUI, git-submodule-sync); `ShellForm`; por último as telas do TestEvidence montadas em código (`CabecalhoSessaoPanel`, `NovaSessaoForm`, `RevisaoForm`), que são as que mais arriscam quebrar comportamento.

## Por que as telas pareciam "feias"

Era o padrão do WinForms sem ajuste: botões em relevo do Windows clássico, fonte padrão (Microsoft Sans Serif 8,25), cores do sistema, controles colados uns nos outros e listas com borda 3D. O tema resolve com:

| Item | Antes | Com o tema |
|---|---|---|
| Fonte | Microsoft Sans Serif 8,25 | Segoe UI 9,75; títulos em Segoe UI Semibold 15 |
| Fundo | Cinza do sistema | `#F5F7FA` na tela, superfícies brancas (`#FFFFFF`) com borda `#DEE2E7` |
| Botão principal | Relevo cinza | Plano, azul da logo `#1170C6`, texto branco, hover `#0E5FA8` |
| Botões secundários | Relevo cinza | Planos, brancos com borda fina |
| Ação destrutiva | Igual aos outros | Branco com texto vermelho `#C62828`, separado à direita |
| Botão desabilitado | Texto cinza sobre a cor original | Neutro (fundo claro, texto cinza) |
| Lista | Borda 3D, linhas baixas | Dentro de um cartão, linhas de 32 px, cabeçalho plano em caixa alta, seleção estilo Explorer |
| Status/resultado | Texto preto | Cor por significado: verde sucesso, vermelho falha, âmbar ressalva, cinza pendente |
| Avisos | Faixa amarela chapada | Fundo `#FFF8E1` com barra de destaque `#F5B400` à esquerda |
| Espaçamento | Variável, 6 px | 20 px nas laterais, 16 px entre blocos, 8 px entre botões |

## Decisões

- **Sem biblioteca de terceiros.** Tudo com controles nativos do WinForms estilizados (`FlatStyle.Flat`, `OwnerDraw` só no cabeçalho da lista, `SetWindowTheme("Explorer")`). Assim não há dependência nova, nem licença, nem troca de controles nos forms.
- **Projeto próprio `src/AppsCm.Visual`** (class library `net8.0-windows`, `UseWindowsForms`), separado do `AppsCm.Compartilhado`. O compartilhado é só dados (Jira, clientes) e não deve depender de WinForms; o visual depende.
- **Tema claro apenas.** A barra flutuante do TestEvidence continua escura de propósito (fica por cima de qualquer sistema, e o escuro chama menos atenção nos prints). Modo escuro geral fica para depois, se alguém pedir.
- **A logo de cada app não muda.** O azul `#1170C6` e o verde `#359862` dos ícones já são a base da paleta.

## Passo a passo

### 1. Criar `src/AppsCm.Visual`
- Mover `Tema.cs` do TestEvidence para `src/AppsCm.Visual/Tema.cs`, trocando o namespace para `AppsCm.Visual`, e separar os painéis em arquivos próprios (`Cartao.cs`, `FaixaComBorda.cs`, `Aviso.cs`).
- Adicionar ao `AppsCm.sln` e ao `.sln` standalone de cada app (para o build standalone continuar funcionando).
- `ProjectReference` a partir do projeto `.UI` de cada app: `..\..\src\AppsCm.Visual\AppsCm.Visual.csproj`. O shell também referencia.
- Apagar `TestEvidence.UI/Visual/` e trocar o `using TestEvidence.UI.Visual;` por `using AppsCm.Visual;`.

### 2. Completar o tema com o que as outras telas vão pedir
O protótipo só cobriu o que a tela principal usa. Acrescentar ao `Tema`:
- `EstilizarCampo(TextBox/ComboBox)`: `BorderStyle.FixedSingle`, altura mínima de 28 px, fundo branco. O `ComboBox DropDownList` com `FlatStyle.Flat`.
- `Rotulo(texto)` e `RotuloSecundario(texto)`: rótulos de formulário em `TextoSecundario`, alinhados acima ou à esquerda do campo de forma consistente.
- `EstilizarAbas(TabControl)`: abas planas (owner-draw com sublinhado azul na aba ativa), usado no shell e nos `ConfigForm`.
- `EstilizarMenu(ContextMenuStrip/MenuStrip)`: `ToolStripProfessionalRenderer` com uma `ProfessionalColorTable` da paleta.
- `EstilizarGrade(DataGridView)`, se algum app usar: mesma linguagem da lista.
- `Dialogo(Form)`: padrão para telas modais, com rodapé `FaixaComBorda` contendo Cancelar (secundário) e a ação principal (primário) à direita, `AcceptButton`/`CancelButton` configurados.

### 3. Aplicar tela por tela
Uma tela por commit, conferindo o comportamento de cada uma (só muda layout e estilo, nunca a lógica).

| App | Tela | Observação |
|---|---|---|
| Shell | `ShellForm` | Aba "Apps": tiles viram `Cartao` com hover `PrimariaSuave` e nome em semibold; `TabControl` com `EstilizarAbas` |
| Shell | Configurações gerais | Já nasce com o tema (fase 0 do plano do TestEvidence) |
| TestEvidence | `MainForm` | Feito no protótipo |
| TestEvidence | `NovaSessaoForm`, `RevisaoForm`, `ConfigForm`, `PromptForm`, `LegendaForm` | `Dialogo` + campos; o `CabecalhoSessaoPanel` é compartilhado entre Nova e Revisão, então estilizar uma vez |
| TestEvidence | `BarraFlutuanteForm`, `SelecaoRegiaoForm` | **Não mudar**: barra escura de propósito; o overlay de seleção não é tela de formulário |
| TestCoverageUI | `MainForm`, `ConfigForm` | Têm `.Designer.cs`: aplicar o tema no construtor do `.cs`, depois do `InitializeComponent()`, sem editar o Designer (o Visual Studio regrava). O log colorido do `MainForm` mantém as cores atuais (elas indicam Passed/Erro) |
| git-submodule-sync | `MainForm`, `ConfigForm`, `PromptForm` | Também com `.Designer.cs`: mesma regra |

Nas telas com Designer, a troca de botão não pode usar as fábricas (`Tema.BotaoPrimario(...)` cria um botão novo). Por isso, criar também versões que estilizam um botão existente: `Tema.ComoPrimario(Button)`, `Tema.ComoSecundario(Button)` e `Tema.ComoPerigo(Button)`. As fábricas passam a chamar essas versões.

### 4. Dentro do shell
Os apps rodam reparenteados nas abas do shell (`TopLevel = false`, sem borda). O tema é aplicado pelo próprio form de cada app, então vale igual nos dois modos. Conferir que o fundo `Fundo` do form preenche a aba inteira, sem sobrar faixa cinza do `TabPage` (se sobrar, o shell aplica `Tema.Fundo` no `TabPage`).

## Cuidados

- **DPI:** o shell roda em modo SystemAware e os forms são montados em código, com tamanhos em pixels. Conferir as telas com 125% e 150% de escala: botões com `AutoSize`, e nada de altura fixa em label de texto.
- **Altura da linha da lista:** vem da `SmallImageList` (truque nativo do ListView). Se uma lista já usa ícones, a imagem precisa ter 32 px de altura, ou ajustar a constante.
- **Cabeçalho da lista:** é owner-draw; itens e subitens continuam com o desenho nativo (`DrawDefault = true`). Não trocar para owner-draw completo sem necessidade: perde a seleção do Explorer.
- **Cores com significado** (verde/vermelho/âmbar) só para status. Nunca para decoração.
- **Fontes:** as `Font` do `Tema` são estáticas e compartilhadas. Nunca chamar `Dispose()` nelas.

## Aceite
- [ ] `AppsCm.Visual` compila e é referenciado pelo shell e pelos três apps; o `TestEvidence.UI/Visual/` não existe mais.
- [ ] Todas as telas da tabela (menos as marcadas "não mudar") com a fonte, os botões e as cores do tema.
- [ ] Nenhuma mudança de comportamento: os mesmos atalhos, botões habilitados nas mesmas condições e as mesmas mensagens.
- [ ] Telas conferidas em 100%, 125% e 150% de escala, dentro do shell e standalone.
- [ ] `.Designer.cs` do TestCoverageUI e do git-submodule-sync sem alteração manual.
- [ ] `dotnet build AppsCm.sln` e os `.sln` de cada app sem erros novos.
