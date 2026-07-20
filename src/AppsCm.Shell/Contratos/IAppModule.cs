namespace AppsCm.Shell.Contratos;

/// <summary>Contrato entre o shell e cada aplicativo hospedado. Implementações vivem no shell
/// (não nos submódulos), para que os submódulos não ganhem dependência do apps-cm e continuem
/// compilando/rodando standalone.</summary>
public interface IAppModule
{
  /// <summary>Identificador estável, usado como chave de aba e como nome da pasta de config
  /// em %LOCALAPPDATA%\apps-cm\&lt;Id&gt;.</summary>
  string Id { get; }

  string NomeExibicao { get; }

  /// <summary>Versão do próprio módulo (independente da versão do bundle do shell).</summary>
  string? Versao { get; }

  /// <summary>Ícone do próprio projeto, usado na aba e no menu. Null se não estiver disponível
  /// (ex.: rodando via F5 antes do primeiro build copiar o arquivo para a pasta de saída).</summary>
  Icon? Icone { get; }

  /// <summary>Cria a view a ser hospedada na aba. Hoje é sempre um Form reparenteado; retornar
  /// Control permite migrar para UserControl no futuro sem mudar o shell.</summary>
  Control CriarView();

  /// <summary>Chamado antes de fechar a aba; o módulo pode recusar (ex.: build em andamento).</summary>
  bool PodeFechar(Control view);
}
