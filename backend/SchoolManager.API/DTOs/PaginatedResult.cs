using System.Text.Json.Serialization;

namespace SchoolManager.API.DTOs;

/// <summary>Envoltorio paginado para listados server-side (PERF-02).
/// Se devuelve cuando el cliente pide page/pageSize; el resto del contrato se
/// conserva (los items son el mismo DTO que antes).</summary>
public sealed class PaginatedResult<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("totalItems")]
    public long TotalItems { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }
}