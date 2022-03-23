﻿using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ToolTypeBase
{
    [Key]
    public int Id { get; set; }

    [MaxLength(64)] public string Name { get; set; } = null!;

    [MaxLength(128)] public string? Description { get; set; }
    
    public int? NamespaceId { get; set; }
}