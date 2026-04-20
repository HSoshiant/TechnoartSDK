using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace TechnoartSDK.Models;

/// <summary>
/// Base record for all persisted entities. Provides Id, CreatedAt, and UpdatedAt.
/// </summary>
public record RepositoryModel
{
    [Key]
    public string Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
