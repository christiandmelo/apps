namespace GitSubmoduleSync.Models;

public enum NivelLog { Detalhe, Info, Sucesso, Aviso, Erro }

public enum StatusProjeto { Pendente, Compilando, Compilado, PuladoSemAlteracao, Falhou, BloqueadoPorDependencia }

public enum StatusSubmodulo { Pendente, Sincronizado, Pulado, BranchNaoEncontrada, WorkingTreeSujo, DivergenciaDeBranch, Erro }

public enum StatusPonteiroSubmodulo { SemSubmodulosFilhos, NadaAAtualizar, PonteiroAtualizado, AlteracaoRealDetectada, FalhaNoPush, Erro }
