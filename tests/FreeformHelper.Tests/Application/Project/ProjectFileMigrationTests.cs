using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ProjectFileMigrationTests
{
    [Fact]
    public void Load_MigratesNullCollections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "schemaVersion": "2",
              "settings": {
                "grid": {
                  "xChannels": 32,
                  "yChannels": 20,
                  "perIcXChannels": null,
                  "perIcYChannels": null,
                  "columnWidths": null,
                  "columnOverrides": null,
                  "rowHeights": null,
                  "rowOverrides": null,
                  "rowWidthOverrides": [ null ],
                  "rowWidthOverrideFlags": [ null ],
                  "columnHeightOverrides": [ null ],
                  "columnHeightOverrideFlags": [ null ]
                },
                "notch": {
                  "enabledVersions": null
                }
              },
              "uiSnapshot": {
                "view": {
                  "layerSelections": null
                }
              },
              "freeformOverrides": null,
              "cadPadCustomValues": null,
              "hiddenCadPadIds": null,
              "dxfCadLayerOverrides": null,
              "dxfCadGeometryOverrides": null,
              "dxfCombinedCadGroups": [
                {
                  "sourceCadIds": null,
                  "outputPads": [
                    {
                      "name": null,
                      "layer": null,
                      "vertices": null
                    }
                  ]
                }
              ],
              "dxfRegularMappingOverrides": null,
              "cadOutputFwDiffIndexOverrides": null,
              "cadOutputFwDiffIndexAnchorCadPadId": null,
              "cadOutputFwDiffIndexAnchorCadPadByIc": null
            }
            """);

            var store = new JsonProjectStore();
            var loaded = JsonProjectStore.Load(path);

            Assert.NotNull(loaded.Settings);
            Assert.NotNull(loaded.Settings.Grid);
            Assert.NotNull(loaded.Settings.Grid.PerIcXChannels);
            Assert.NotNull(loaded.Settings.Grid.PerIcYChannels);
            Assert.NotNull(loaded.Settings.Grid.ColumnWidths);
            Assert.NotNull(loaded.Settings.Grid.ColumnOverrides);
            Assert.NotNull(loaded.Settings.Grid.RowHeights);
            Assert.NotNull(loaded.Settings.Grid.RowOverrides);
            Assert.NotNull(loaded.Settings.Grid.RowWidthOverrides);
            Assert.NotNull(loaded.Settings.Grid.RowWidthOverrideFlags);
            Assert.NotNull(loaded.Settings.Grid.ColumnHeightOverrides);
            Assert.NotNull(loaded.Settings.Grid.ColumnHeightOverrideFlags);
            Assert.Equal(GridSettings.DefaultChannelLimit, loaded.Settings.Grid.MaxChannelsX);
            Assert.Equal(GridSettings.DefaultChannelLimit, loaded.Settings.Grid.MaxChannelsY);
            Assert.All(loaded.Settings.Grid.RowWidthOverrides, inner => Assert.NotNull(inner));
            Assert.All(loaded.Settings.Grid.RowWidthOverrideFlags, inner => Assert.NotNull(inner));
            Assert.All(loaded.Settings.Grid.ColumnHeightOverrides, inner => Assert.NotNull(inner));
            Assert.All(loaded.Settings.Grid.ColumnHeightOverrideFlags, inner => Assert.NotNull(inner));
            Assert.NotNull(loaded.Settings.Notch);
            Assert.NotNull(loaded.Settings.Notch.EnabledVersions);
            Assert.Equal(NotchComputationMode.CadAllocation, loaded.Settings.Notch.ComputationMode);
            Assert.Contains(Domain.Notch.NotchAlgorithmVersion.V21, loaded.Settings.Notch.EnabledVersions);
            Assert.Contains(Domain.Notch.NotchAlgorithmVersion.V22, loaded.Settings.Notch.EnabledVersions);

            Assert.NotNull(loaded.UiSnapshot);
            Assert.NotNull(loaded.UiSnapshot.View);
            Assert.NotNull(loaded.UiSnapshot.View.LayerSelections);

            Assert.NotNull(loaded.FreeformOverrides);
            Assert.NotNull(loaded.CadPadCustomValues);
            Assert.NotNull(loaded.HiddenCadPadIds);
            Assert.NotNull(loaded.DxfCadLayerOverrides);
            Assert.NotNull(loaded.DxfCadGeometryOverrides);
            Assert.NotNull(loaded.DxfCombinedCadGroups);
            var combinedGroup = Assert.Single(loaded.DxfCombinedCadGroups);
            Assert.NotNull(combinedGroup.SourceCadIds);
            var outputPad = Assert.Single(combinedGroup.OutputPads);
            Assert.NotNull(outputPad.Name);
            Assert.NotNull(outputPad.Layer);
            Assert.NotNull(outputPad.Vertices);
            Assert.NotNull(loaded.DxfRegularMappingOverrides);
            Assert.NotNull(loaded.CadOutputFwDiffIndexOverrides);
            Assert.NotNull(loaded.CadOutputFwDiffIndexAnchorCadPadByIc);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_MigratesLegacyChannelLimitAndPreservesLargeChannelCounts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "schemaVersion": "2",
              "settings": {
                "grid": {
                  "xChannels": 300,
                  "yChannels": 400,
                  "maxChannelsX": 256,
                  "maxChannelsY": 256
                }
              }
            }
            """);

            var store = new JsonProjectStore();
            var loaded = JsonProjectStore.Load(path);

            Assert.Equal(300, loaded.Settings.Grid.XChannels);
            Assert.Equal(400, loaded.Settings.Grid.YChannels);
            Assert.Equal(GridSettings.DefaultChannelLimit, loaded.Settings.Grid.MaxChannelsX);
            Assert.Equal(GridSettings.DefaultChannelLimit, loaded.Settings.Grid.MaxChannelsY);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_DropsUnsupportedLegacyNotchVersions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "schemaVersion": "2",
              "settings": {
                "notch": {
                  "enabledVersions": [30, 31]
                }
              },
              "uiSnapshot": {
                "notch": {
                  "enabledVersions": ["v3.0", "v3.1"]
                }
              }
            }
            """);

            var loaded = JsonProjectStore.Load(path);

            Assert.Single(loaded.Settings.Notch.EnabledVersions);
            Assert.Contains(Domain.Notch.NotchAlgorithmVersion.V22, loaded.Settings.Notch.EnabledVersions);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task LoadAndSave_PreservesUnknownRootProperties()
    {
        var input = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}_in.json");
        var output = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}_out.json");
        try
        {
            File.WriteAllText(input, """
            {
              "schemaVersion": "2",
              "legacyFoo": { "bar": 1 },
              "settings": {
                "grid": { "xChannels": 32, "yChannels": 20 }
              }
            }
            """);

            var store = new JsonProjectStore();
            var loaded = JsonProjectStore.Load(input);
            await store.SaveAsync(output, loaded, CancellationToken.None);

            var outJson = File.ReadAllText(output);
            Assert.Contains("\"legacyFoo\"", outJson);
        }
        finally
        {
            if (File.Exists(input))
            {
                File.Delete(input);
            }
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesDxfRegularMappingOverrides()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var file = new ProjectFile
            {
                DxfRegularMappingOverrides = new System.Collections.Generic.Dictionary<int, int>
                {
                    [101] = 7,
                    [202] = 18,
                }
            };
            var store = new JsonProjectStore();
            await store.SaveAsync(path, file, CancellationToken.None);
            var loaded = JsonProjectStore.Load(path);

            Assert.Equal(2, loaded.DxfRegularMappingOverrides.Count);
            Assert.Equal(7, loaded.DxfRegularMappingOverrides[101]);
            Assert.Equal(18, loaded.DxfRegularMappingOverrides[202]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesCadOutputFwDiffIndexOverrides()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var file = new ProjectFile
            {
                CadOutputFwDiffIndexOverrides = new System.Collections.Generic.Dictionary<int, int>
                {
                    [101] = 10,
                    [202] = 25,
                }
            };
            var store = new JsonProjectStore();
            await store.SaveAsync(path, file, CancellationToken.None);
            var loaded = JsonProjectStore.Load(path);

            Assert.Equal(2, loaded.CadOutputFwDiffIndexOverrides.Count);
            Assert.Equal(10, loaded.CadOutputFwDiffIndexOverrides[101]);
            Assert.Equal(25, loaded.CadOutputFwDiffIndexOverrides[202]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesDxfCadGeometryOverrides()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var file = new ProjectFile
            {
                DxfCadGeometryOverrides = new System.Collections.Generic.Dictionary<int, ProjectCadGeometrySnapshot>
                {
                    [101] = new ProjectCadGeometrySnapshot
                    {
                        Vertices =
                        [
                            new FreeformHelper.Domain.Geometry.Point2(0, 0),
                            new FreeformHelper.Domain.Geometry.Point2(10, 0),
                            new FreeformHelper.Domain.Geometry.Point2(10, 20),
                            new FreeformHelper.Domain.Geometry.Point2(0, 20),
                        ],
                    },
                },
            };
            var store = new JsonProjectStore();
            await store.SaveAsync(path, file, CancellationToken.None);
            var loaded = JsonProjectStore.Load(path);

            Assert.Single(loaded.DxfCadGeometryOverrides);
            Assert.Equal(4, loaded.DxfCadGeometryOverrides[101].Vertices.Count);
            Assert.Equal(10, loaded.DxfCadGeometryOverrides[101].Vertices[1].X, 6);
            Assert.Equal(20, loaded.DxfCadGeometryOverrides[101].Vertices[2].Y, 6);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesCadOutputFwDiffIndexAnchorCadPadId()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var file = new ProjectFile
            {
                CadOutputFwDiffIndexAnchorCadPadId = 314159
            };
            var store = new JsonProjectStore();
            await store.SaveAsync(path, file, CancellationToken.None);
            var loaded = JsonProjectStore.Load(path);

            Assert.Equal(314159, loaded.CadOutputFwDiffIndexAnchorCadPadId);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesCadOutputFwDiffIndexAnchorCadPadByIc()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_project_{Guid.NewGuid():N}.json");
        try
        {
            var file = new ProjectFile
            {
                CadOutputFwDiffIndexAnchorCadPadByIc = new System.Collections.Generic.Dictionary<int, int>
                {
                    [0] = 101,
                    [1] = 202,
                }
            };
            var store = new JsonProjectStore();
            await store.SaveAsync(path, file, CancellationToken.None);
            var loaded = JsonProjectStore.Load(path);

            Assert.Equal(2, loaded.CadOutputFwDiffIndexAnchorCadPadByIc.Count);
            Assert.Equal(101, loaded.CadOutputFwDiffIndexAnchorCadPadByIc[0]);
            Assert.Equal(202, loaded.CadOutputFwDiffIndexAnchorCadPadByIc[1]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
