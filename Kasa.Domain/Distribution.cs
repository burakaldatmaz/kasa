namespace Kasa.Domain;

/// <summary>Ay sonu bakiyesinin ortak dağıtımı. Partner1 + Partner2 DAİMA bakiyeye eşittir.</summary>
public readonly record struct Distribution(Money Partner1, Money Partner2);
