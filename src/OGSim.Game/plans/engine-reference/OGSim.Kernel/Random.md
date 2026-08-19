# Random

Source: `src\OGSim.Kernel\Random.cs` · Lines: 37

## File intent

> SDD-001 §4 — seeded, per-subsystem streams. Adding a draw in one stream can
> never shift another (design 11 §3.1). PCG64; stream seed derived from the
> world seed by a stable hash of the stream name.
> <summary>The eight streams (SDD-013 §2 persists all eight positions).</summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L8` `public enum StreamId`
- `L13` `public interface IRandomSource`
- `L18` `public interface IRandomStream`

## Accessible members

_No public/internal/protected/private member lines matched the extractor._

