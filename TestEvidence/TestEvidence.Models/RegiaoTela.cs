namespace TestEvidence.Models;

/// <summary>Retângulo em pixels físicos, coordenadas da tela virtual (pode ter X/Y negativos).</summary>
public sealed record RegiaoTela(int X, int Y, int Largura, int Altura);

public sealed record PosicaoTela(int X, int Y);
