using System.Drawing;
using AppsCm.Shell.Atualizacao;
using AppsCm.Shell.Contratos;
using AppsCm.Shell.Modulos;

namespace AppsCm.Shell;

public partial class ShellForm : Form
{
  private static readonly IAppModule[] Modulos =
  [
    new ModuloTestCoverage(),
    new ModuloGitSubmoduleSync(),
  ];

  private readonly TabControl _tabControl;
  private readonly ImageList _iconesModulos = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
  private readonly Dictionary<string, TabPage> _abas = new();
  private readonly ContextMenuStrip _menuContextoAba;

  public ShellForm()
  {
    Text = "apps-cm";
    ClientSize = new Size(1400, 900);
    MinimumSize = new Size(900, 700);
    StartPosition = FormStartPosition.CenterScreen;
    WindowState = FormWindowState.Maximized;

    try
    {
      Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
    }
    catch (ArgumentException)
    {
      // PublishSingleFile pode deixar ExecutablePath vazio; ícone não é crítico.
    }

    foreach (var modulo in Modulos)
    {
      if (modulo.Icone is { } icone)
        _iconesModulos.Images.Add(modulo.Id, icone);
    }

    var menuPrincipal = new MenuStrip { Dock = DockStyle.Top };
    var mnuApps = new ToolStripMenuItem("Apps");
    foreach (var modulo in Modulos)
    {
      var item = new ToolStripMenuItem(modulo.NomeExibicao);
      if (modulo.Icone is { } icone)
        item.Image = icone.ToBitmap();
      item.Click += (_, _) => AbrirModulo(modulo);
      mnuApps.DropDownItems.Add(item);
    }
    menuPrincipal.Items.Add(mnuApps);
    MainMenuStrip = menuPrincipal;

    _tabControl = new TabControl { Dock = DockStyle.Fill, ImageList = _iconesModulos };

    _menuContextoAba = new ContextMenuStrip();
    var itemFechar = new ToolStripMenuItem("Fechar aba");
    itemFechar.Click += (_, _) =>
    {
      if (_tabControl.SelectedTab is { } aba) FecharAba(aba);
    };
    _menuContextoAba.Items.Add(itemFechar);
    _tabControl.MouseUp += TabControl_MouseUp;
    _tabControl.ContextMenuStrip = _menuContextoAba;

    Controls.Add(_tabControl);
    Controls.Add(menuPrincipal);

    LimparArquivosAntigosDoUpdater();
    Load += async (_, _) => await VerificarAtualizacaoAsync();
  }

  // O Updater se autoatualiza renomeando o próprio exe em uso para *.old (Windows bloqueia
  // deletar um exe rodando, mas permite renomear); o shell é quem limpa esses restos depois.
  private static void LimparArquivosAntigosDoUpdater()
  {
    try
    {
      foreach (var antigo in Directory.GetFiles(AppContext.BaseDirectory, "*.old"))
        File.Delete(antigo);
    }
    catch (IOException)
    {
      // Best-effort; tentar de novo no próximo start não tem custo.
    }
  }

  private async Task VerificarAtualizacaoAsync()
  {
    var updater = new UpdateService();

    try
    {
      var updateInfo = await updater.CheckForUpdateAsync();
      if (updateInfo is null) return;

      var resultado = MessageBox.Show(
        $"Nova versão disponível ({updateInfo.version}). Deseja atualizar agora?",
        "apps-cm — Atualização", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

      if (resultado == DialogResult.Yes)
        await updater.DownloadAndRunUpdaterAsync(updateInfo);
    }
    catch (Exception ex) when (ex is FileNotFoundException or IOException or System.Net.Http.HttpRequestException)
    {
      MessageBox.Show(this, $"Erro ao baixar a atualização: {ex.Message}",
        "apps-cm — Atualização", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void TabControl_MouseUp(object? sender, MouseEventArgs e)
  {
    if (e.Button != MouseButtons.Right) return;

    for (var i = 0; i < _tabControl.TabPages.Count; i++)
    {
      if (_tabControl.GetTabRect(i).Contains(e.Location))
      {
        _tabControl.SelectedIndex = i;
        return;
      }
    }
  }

  private void AbrirModulo(IAppModule modulo)
  {
    if (_abas.TryGetValue(modulo.Id, out var existente))
    {
      _tabControl.SelectedTab = existente;
      return;
    }

    var aba = new TabPage(modulo.NomeExibicao) { Tag = modulo };
    if (_iconesModulos.Images.ContainsKey(modulo.Id))
      aba.ImageKey = modulo.Id;

    var view = modulo.CriarView();
    HospedeiroDeApp.Hospedar(view, aba);

    _tabControl.TabPages.Add(aba);
    _abas[modulo.Id] = aba;
    _tabControl.SelectedTab = aba;
  }

  private void FecharAba(TabPage aba)
  {
    var modulo = (IAppModule)aba.Tag!;
    var view = aba.Controls[0];

    if (!modulo.PodeFechar(view)) return;
    if (!HospedeiroDeApp.Encerrar(view)) return;

    _tabControl.TabPages.Remove(aba);
    _abas.Remove(modulo.Id);
    aba.Dispose();
  }
}
