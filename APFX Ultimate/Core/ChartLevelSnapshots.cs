namespace cAlgo.Robots;

public readonly record struct PsychologicalLevelsSnapshot(double Below, double Above);

public readonly record struct TradingSessionSnapshot(string Name, double Low, double High);

public readonly record struct StrongLevelsSnapshot(double? Support, double? Resistance);
