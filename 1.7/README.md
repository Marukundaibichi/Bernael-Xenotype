# RimWorld 1.7

The 1.7 build shares the existing C# sources, Defs, translations and assets. Its
load folder comes after `1.6`, overriding only `Assemblies/BernaelXenotype.dll`
and adding five native ShaderDefs, their GLSL sources and the original smoke
texture. The 1.6 assembly and bundles remain available for 1.6.

The migration preserves gameplay and save fields. `RIMWORLD_1_7` selects native
shaders instead of the Unity bundles; the existing rendering and capture APIs
are retained where the 1.7 compatibility implementation works. Shader algorithms,
timing, blending and render queues follow the original ShaderLab effects.

## Building

Requires the .NET 10 SDK, RimWorld 1.7 and Harmony's 1.7 assembly:

```powershell
dotnet build 1.7/Source/BernaelXenotype.csproj -c Release
```

Paths default to this mod being installed under `RimWorld/Mods`. Elsewhere, pass
`-p:GameDir="path/to/RimWorld"` and, if needed, `-p:HarmonyPath="path/to/0Harmony.dll"`.
Rebuild this output after changing shared C# sources. The existing GitHub workflow
builds 1.6 using NuGet references and packages the committed 1.7 assembly; it does
not have the local game references needed to rebuild 1.7.

## Validation

Checked on 2026-10-28 against RimWorld **1.7.4990 rev1069**, Harmony **3.0.0-rc.3**:

- 1.6 and 1.7 release compilation: no errors or warnings.
- All five ShaderDefs: material layouts and seven shader stages compile using
  the game's shader compiler; a deliberately invalid shader is rejected.
- Isolated startup and new game with Harmony and all installed DLC: no additional
  loading errors compared with the dependency-only baseline.
- Rendered game: one 1.7 assembly, 19 main Harmony patches, pawn capture, full-pawn
  ashen shading, Mirage, Malicious Seal, Dire Orb flight/burst and an actual
  Soul Drain cast. The final run passed 23 assertions.
- Save/reload: soul resource, seal, Mirage caster/snapshot, the active Soul Drain
  channel/snapshot and the Dire Orb burst state preserved.

Local reports and screenshots are under the ignored `QA/render-check3/` directory.
This was not
a full gameplay suite or a 1.6/1.7 audio comparison; optional VEF/VPE and
Nephilim/EBSG combinations were not included in these runs.
