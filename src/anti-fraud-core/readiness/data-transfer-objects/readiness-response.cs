namespace AntiFraud.Core.Readiness.DataTransferObjects;

/// <summary>Resposta tipada para o endpoint <c>/ready</c> (compatível com Native AOT / source-gen).</summary>
public sealed record ReadinessResponse(string Status);
