using AppsCm.Visual;
using TestEvidence.Models;

namespace TestEvidence.UI;

/// <summary>Campos do cabeçalho da sessão, compartilhados entre Nova sessão e Revisão.</summary>
public sealed class CabecalhoSessaoPanel : TableLayoutPanel
{
  private readonly TextBox _tarefa = new() { Dock = DockStyle.Fill };
  private readonly TextBox _titulo = new() { Dock = DockStyle.Fill };
  private readonly TextBox _requisito = Multilinha(60);
  private readonly TextBox _cenario = Multilinha(60);
  private readonly ComboBox _ambiente = new() { Dock = DockStyle.Fill };
  private readonly TextBox _testador = new() { Dock = DockStyle.Fill };
  private readonly TextBox? _etapas;

  public CabecalhoSessaoPanel(bool comEtapas)
  {
    ColumnCount = 2;
    Dock = DockStyle.Top;
    AutoSize = true;
    Padding = Padding.Empty;
    ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
    ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

    Linha("Tarefa *", _tarefa, 26);
    Linha("Título", _titulo, 26);
    Linha("Requisito", _requisito, 66);
    Linha("Cenário / Pré-condições", _cenario, 66);
    Linha("Ambiente", _ambiente, 26);
    Linha("Testador", _testador, 26);
    if (comEtapas)
    {
      _etapas = Multilinha(66);
      Linha("Etapas (uma por linha)", _etapas, 70);
    }
  }

  private static TextBox Multilinha(int altura) => new()
  {
    Multiline = true,
    AcceptsReturn = true,
    ScrollBars = ScrollBars.Vertical,
    Dock = DockStyle.Fill,
    Height = altura
  };

  private void Linha(string rotulo, Control campo, int altura)
  {
    RowStyles.Add(new RowStyle(SizeType.Absolute, altura + 10));
    var lbl = Tema.Rotulo(rotulo);
    lbl.AutoSize = false;
    lbl.Dock = DockStyle.Fill;
    lbl.TextAlign = ContentAlignment.MiddleLeft;
    Tema.EstilizarCampo(campo);
    Controls.Add(lbl);
    Controls.Add(campo);
  }

  public string Tarefa => _tarefa.Text.Trim();

  public void Carregar(SessaoEvidencia s, IEnumerable<string> ambientes)
  {
    _tarefa.Text = s.Tarefa;
    _titulo.Text = s.Titulo;
    _requisito.Text = s.Requisito;
    _cenario.Text = s.Cenario;
    _ambiente.Items.Clear();
    _ambiente.Items.AddRange(ambientes.Cast<object>().ToArray());
    _ambiente.Text = s.Ambiente;
    _testador.Text = s.Testador;
    if (_etapas != null) _etapas.Text = string.Join(Environment.NewLine, s.Etapas);
  }

  public void Aplicar(SessaoEvidencia s)
  {
    s.Tarefa = _tarefa.Text.Trim();
    s.Titulo = _titulo.Text.Trim();
    s.Requisito = _requisito.Text.Trim();
    s.Cenario = _cenario.Text.Trim();
    s.Ambiente = _ambiente.Text.Trim();
    s.Testador = _testador.Text.Trim();
    if (_etapas != null)
    {
      s.Etapas = _etapas.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();
      if (s.Etapas.Count == 0) s.Etapas.Add("Evidências");
      if (!s.Etapas.Contains(s.EtapaAtual)) s.EtapaAtual = s.Etapas[0];
    }
  }
}
