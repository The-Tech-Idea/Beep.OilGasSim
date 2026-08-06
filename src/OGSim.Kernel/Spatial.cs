// SDD-001 §1.4 — spatial primitives (concept G17). Fictional basins on a flat
// plane (open decision W1): no geodesy, ever. Polygon algorithms (shoelace
// area, ray-cast containment) arrive with R1 implementation against these
// shapes; network distance lives on the generated transport graph (OGSim.World).

using System.Collections.Immutable;

namespace OGSim.Kernel;

/// <summary>Metres on the world plane.</summary>
public readonly record struct Coordinate(double X, double Y);

/// <summary>
/// An immutable ring: CCW, non-self-intersecting (validated at construction in
/// R1). Data shape only at the contract stage.
/// </summary>
public readonly record struct Polygon(ImmutableArray<Coordinate> Vertices);
