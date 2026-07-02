namespace Kasa.Domain;

/// <summary>Tarihli işlem satırı: devir zinciri hesabında hangi güne ait olduğu bilinmeli.</summary>
public readonly record struct DatedTxnLine(DateOnly Date, TxnLine Line);
