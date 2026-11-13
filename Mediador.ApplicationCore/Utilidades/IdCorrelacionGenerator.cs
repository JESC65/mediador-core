namespace Mediador.ApplicationCore.Utilidades;

/// <summary>
/// Generador de identificadores de correlación estables y únicos sin fecha (R5).
/// Cumple con la recomendación de R4/R5 (UUID v7 cuando el runtime lo soporta, o UUID v4 estándar).
/// </summary>
public static class IdCorrelacionGenerator
{
    private static readonly System.Reflection.MethodInfo? CreateVersion7Method =
        typeof(Guid).GetMethod("CreateVersion7", Type.EmptyTypes);

    public static string Generar()
    {
        if (CreateVersion7Method is not null)
        {
            return CreateVersion7Method.Invoke(null, null)?.ToString() ?? Guid.NewGuid().ToString();
        }
        return Guid.NewGuid().ToString();
    }
}
