using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyTts.Data.Entities
{
    [Table("Haberler_Speeches")]
    public class Mp3Meta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Column("FileName")]
        public required string FileName { get; set; }
        [Column("Title")]
        public required string Title { get; set; }
        [Column("Size")]
        public double Size { get; set; }
        [Column("Duration")]
        public int Duration { get; set; }
        [Column("BitRate")]
        public int BitRate { get; set; }
        [Column("NewsCount")]
        public int NewsCount { get; set; }
        [Column("FilePath")]
        public required string FilePath { get; set; } // Ensure this property is correctly defined
        [Column("FullUrl")]
        public required string FullUrl { get; set; }
        [Column("NewsIds")]
        public required string NewsIds { get; set; }
        [Column("Language")]
        public required string Language { get; set; }

        [Column("Created_Date")]
        public DateTime? CreatedDate { get; set; }

        // Add other properties as needed, based on your database schema
    }
}