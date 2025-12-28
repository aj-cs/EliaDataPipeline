using System;
using System.ComponentModel.DataAnnotations;

namespace Elia.Shared.Models;
public class RawData
{
    [Key]
    public int Id { get; set; }
    public string Source { get; set; } = default;
    public DateTime FetchedAt { get; set; }

    // from Elia
    [Required]
    public string Payload { get; set; } = default;
    public bool Processed { get; set; } = false;
}

