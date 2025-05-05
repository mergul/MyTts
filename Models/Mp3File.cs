using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyTts.Models
{
    [Table("Haberler_Speeches")]
    public class Mp3File
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Column("FileName")]
        public string FileName { get; set; }
        [Column("Title")]
        public string Title { get; set; }
        [Column("Size")]
        public double Size { get; set; }
        [Column("Duration")]
        public int Duration { get; set; }
        [Column("BitRate")]
        public int BitRate { get; set; }
        [Column("NewsCount")]
        public int NewsCount { get; set; }
        [Column("FilePath")]
        public string FilePath { get; set; } // Ensure this property is correctly defined
        [Column("FullUrl")]
        public string FullUrl { get; set; }
        [Column("NewsIds")]
        public string NewsIds { get; set; }
        [Column("Language")]
        public string Language { get; set; }

        [Column("Created_Date")]
        public DateTime? CreatedDate { get; set; }

        // Add other properties as needed, based on your database schema
    }
}