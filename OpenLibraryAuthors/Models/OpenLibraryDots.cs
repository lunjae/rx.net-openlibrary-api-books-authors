using System.Text.Json.Serialization;

namespace OpenLibraryAuthors.Models;

// Json odgovor sa API-ja, docs je lista knjiga
public class OpenLibrarySearchResponse
{
    [JsonPropertyName("docs")] 
    public List<OpenLibraryDoc> Docs { get; set; } = [];
}

// Jedna knjiga iz te liste, deserijalizacija jer API korisi stnake_case
public class OpenLibraryDoc
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }
    
    [JsonPropertyName("language")]
    public string[]? Languages { get; set; }
    
    [JsonPropertyName("rating_average")]
    public double? RatingsAverage { get; set; }
}