namespace Planarian.Modules.Caves.Models;

public sealed record CaveEditAuthoringContextVm(
    CaveVm Cave,
    IReadOnlyList<GeoJsonUploadVm> LinePlots);
