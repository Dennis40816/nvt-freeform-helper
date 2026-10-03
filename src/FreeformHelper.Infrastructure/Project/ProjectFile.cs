using System.Text.Json;
using System.Text.Json.Serialization;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Infrastructure.Project;

public sealed class ProjectFile
{
    private Dictionary<int, FreeformType> _freeformOverrides = new();
    private Dictionary<int, double> _cadPadCustomValues = new();
    private HashSet<int> _hiddenCadPadIds = new();
    private HashSet<int> _visibleDuplicateCadPadIds = new();
    private Dictionary<int, string> _dxfCadLayerOverrides = new();
    private Dictionary<int, ProjectCadGeometrySnapshot> _dxfCadGeometryOverrides = new();
    private List<ProjectDxfCombinedCadGroup> _dxfCombinedCadGroups = new();
    private Dictionary<int, int> _dxfRegularMappingOverrides = new();
    private Dictionary<int, int> _cadOutputFwDiffIndexOverrides = new();
    private Dictionary<int, int> _cadOutputFwDiffIndexAnchorCadPadByIc = new();
    private Dictionary<string, JsonElement>? _extensionData;

    public string SchemaVersion { get; set; } = ProjectSchema.CurrentVersion;

    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? LastDxfPath { get; set; }

    public string? EmbeddedDxfName { get; set; }

    public byte[]? EmbeddedDxf { get; set; }

    public string? EmbeddedRegularVisibilityMaskName { get; set; }

    public byte[]? EmbeddedRegularVisibilityMask { get; set; }

    public ProjectSettings Settings { get; set; } = new();

    public ProjectUiSnapshot UiSnapshot { get; set; } = new();

    public Dictionary<int, FreeformType> FreeformOverrides
    {
        get => _freeformOverrides;
        set => _freeformOverrides = CopyOrEmpty(value);
    }

    public Dictionary<int, double> CadPadCustomValues
    {
        get => _cadPadCustomValues;
        set => _cadPadCustomValues = CopyOrEmpty(value);
    }

    public HashSet<int> HiddenCadPadIds
    {
        get => _hiddenCadPadIds;
        set => _hiddenCadPadIds = CopyOrEmpty(value);
    }

    public HashSet<int> VisibleDuplicateCadPadIds
    {
        get => _visibleDuplicateCadPadIds;
        set => _visibleDuplicateCadPadIds = CopyOrEmpty(value);
    }

    public Dictionary<int, string> DxfCadLayerOverrides
    {
        get => _dxfCadLayerOverrides;
        set => _dxfCadLayerOverrides = CopyOrEmpty(value);
    }

    public Dictionary<int, ProjectCadGeometrySnapshot> DxfCadGeometryOverrides
    {
        get => _dxfCadGeometryOverrides;
        set => _dxfCadGeometryOverrides = CopyOrEmpty(value);
    }

    public List<ProjectDxfCombinedCadGroup> DxfCombinedCadGroups
    {
        get => _dxfCombinedCadGroups;
        set => _dxfCombinedCadGroups = CopyOrEmpty(value);
    }

    public Dictionary<int, int> DxfRegularMappingOverrides
    {
        get => _dxfRegularMappingOverrides;
        set => _dxfRegularMappingOverrides = CopyOrEmpty(value);
    }

    public Dictionary<int, int> CadOutputFwDiffIndexOverrides
    {
        get => _cadOutputFwDiffIndexOverrides;
        set => _cadOutputFwDiffIndexOverrides = CopyOrEmpty(value);
    }

    [JsonPropertyName("dxfVisibleIndexOverrides")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, int>? DxfVisibleIndexOverrides
    {
        get => null;
        set
        {
            if (value is not null && _cadOutputFwDiffIndexOverrides.Count == 0)
            {
                _cadOutputFwDiffIndexOverrides = CopyOrEmpty(value);
            }
        }
    }

    public int? CadOutputFwDiffIndexAnchorCadPadId { get; set; }

    [JsonPropertyName("dxfVisibleIndexAnchorCadPadId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DxfVisibleIndexAnchorCadPadId
    {
        get => null;
        set
        {
            if (!CadOutputFwDiffIndexAnchorCadPadId.HasValue)
            {
                CadOutputFwDiffIndexAnchorCadPadId = value;
            }
        }
    }

    public Dictionary<int, int> CadOutputFwDiffIndexAnchorCadPadByIc
    {
        get => _cadOutputFwDiffIndexAnchorCadPadByIc;
        set => _cadOutputFwDiffIndexAnchorCadPadByIc = CopyOrEmpty(value);
    }

    [JsonPropertyName("dxfVisibleIndexAnchorCadPadByIc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, int>? DxfVisibleIndexAnchorCadPadByIc
    {
        get => null;
        set
        {
            if (value is not null && _cadOutputFwDiffIndexAnchorCadPadByIc.Count == 0)
            {
                _cadOutputFwDiffIndexAnchorCadPadByIc = CopyOrEmpty(value);
            }
        }
    }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData
    {
        get => _extensionData;
        set => _extensionData = CopyOrNull(value);
    }

    private static Dictionary<TKey, TValue> CopyOrEmpty<TKey, TValue>(Dictionary<TKey, TValue>? value)
        where TKey : notnull
        => value is null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(value);

    private static HashSet<T> CopyOrEmpty<T>(HashSet<T>? value)
        => value is null ? new HashSet<T>() : new HashSet<T>(value);

    private static List<T> CopyOrEmpty<T>(List<T>? value)
        => value is null ? new List<T>() : new List<T>(value);

    private static Dictionary<string, JsonElement>? CopyOrNull(Dictionary<string, JsonElement>? value)
        => value is null ? null : new Dictionary<string, JsonElement>(value);
}
