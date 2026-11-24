using System.Text.Json;
using System.Drawing;
using System.Drawing.Imaging;
using TestEvidence.Models;

namespace TestEvidence.Services;

public sealed class SessaoRepository
{
  private static readonly JsonSerializerOptions Opcoes = new()
  {
    WriteIndented = true,
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
  };

  private readonly string _raiz;

  public SessaoRepository() : this(CaminhoConfig.PastaSessoes) { }

  public SessaoRepository(string raiz) => _raiz = raiz;

  public SessaoEvidencia Criar(SessaoEvidencia dados)
  {
    var agora = DateTime.Now;
    dados.Inicio = agora;
    dados.Status = StatusSessao.EmAndamento;
    var baseId = $"{agora:yyyyMMdd-HHmmss}-{Sanitizar(dados.Tarefa)}";
    var id = baseId;
    for (var i = 2; Directory.Exists(Path.Combine(_raiz, id)); i++)
      id = $"{baseId}-{i}";
    dados.Id = id;
    Directory.CreateDirectory(Path.Combine(_raiz, id, "img"));
    Salvar(dados);
    return dados;
  }

  public void Salvar(SessaoEvidencia sessao)
  {
    var pasta = PastaDa(sessao);
    Directory.CreateDirectory(pasta);
    var arquivo = Path.Combine(pasta, "sessao.json");
    var tmp = arquivo + ".tmp";
    File.WriteAllText(tmp, JsonSerializer.Serialize(sessao, Opcoes));
    File.Move(tmp, arquivo, overwrite: true);
  }

  public SessaoEvidencia? Carregar(string id)
  {
    try
    {
      var arquivo = Path.Combine(_raiz, id, "sessao.json");
      if (!File.Exists(arquivo)) return null;
      var s = JsonSerializer.Deserialize<SessaoEvidencia>(File.ReadAllText(arquivo), Opcoes);
      if (s != null) s.Id = id;
      return s;
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
      return null;
    }
  }

  public IReadOnlyList<SessaoEvidencia> Listar()
  {
    if (!Directory.Exists(_raiz)) return Array.Empty<SessaoEvidencia>();
    return Directory.EnumerateDirectories(_raiz)
      .Select(d => Carregar(Path.GetFileName(d)))
      .OfType<SessaoEvidencia>()
      .OrderByDescending(s => s.Inicio)
      .ToList();
  }

  public string PastaDa(SessaoEvidencia sessao) => Path.Combine(_raiz, sessao.Id);

  public string CaminhoImagem(SessaoEvidencia sessao, Evidencia evidencia) =>
    Path.Combine(PastaDa(sessao), evidencia.ArquivoImagem);

  public string AdicionarImagem(SessaoEvidencia sessao, Bitmap imagem)
  {
    var pastaImg = Path.Combine(PastaDa(sessao), "img");
    Directory.CreateDirectory(pastaImg);
    var numero = 1;
    foreach (var e in sessao.Evidencias)
    {
      if (int.TryParse(Path.GetFileNameWithoutExtension(e.ArquivoImagem), out var n) && n >= numero)
        numero = n + 1;
    }
    while (File.Exists(Path.Combine(pastaImg, $"{numero:000}.png"))) numero++;
    var relativo = Path.Combine("img", $"{numero:000}.png");
    imagem.Save(Path.Combine(PastaDa(sessao), relativo), ImageFormat.Png);
    return relativo;
  }

  public void ExcluirEvidencia(SessaoEvidencia sessao, Evidencia evidencia)
  {
    sessao.Evidencias.Remove(evidencia);
    Reordenar(sessao);
    Salvar(sessao);
    try
    {
      var arq = CaminhoImagem(sessao, evidencia);
      if (File.Exists(arq)) File.Delete(arq);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      // imagem em uso: fica órfã na pasta, sem afetar a sessão
    }
  }

  public static void Reordenar(SessaoEvidencia sessao)
  {
    var i = 1;
    foreach (var e in sessao.Evidencias.OrderBy(x => x.Ordem).ToList())
      e.Ordem = i++;
  }

  public void ExcluirSessao(string id)
  {
    var pasta = Path.Combine(_raiz, id);
    if (!Directory.Exists(pasta)) return;
    try
    {
      Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(pasta,
        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
    {
      Directory.Delete(pasta, true);
    }
  }

  private static string Sanitizar(string tarefa)
  {
    var invalidos = Path.GetInvalidFileNameChars();
    var limpo = new string(tarefa.Trim().Select(c => invalidos.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());
    if (limpo.Length > 40) limpo = limpo[..40];
    return limpo.Length == 0 ? "sem-tarefa" : limpo;
  }
}
