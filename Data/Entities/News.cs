using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyTts.Data.Entities
{
    [Table("News")]
    public class News
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("haberId")]
        public int? HaberId { get; set; }

        [Column("title")]
        public string? Title { get; set; }

        [Column("spot")]
        public string? Spot { get; set; }

        [Column("head")]
        public string? Head { get; set; }

        // Getter and Setter for Id
        public int GetId()
        {
            return Id;
        }

        public void SetId(int id)
        {
            Id = id;
        }

        // Getter and Setter for HaberId
        public int? GetHaberId()
        {
            return HaberId;
        }

        public void SetHaberId(int? haberId)
        {
            HaberId = haberId;
        }

        // Getter and Setter for Title
        public string? GetTitle()
        {
            return Title;
        }

        public void SetTitle(string? title)
        {
            Title = title;
        }

        // Getter and Setter for Spot
        public string? GetSpot()
        {
            return Spot;
        }

        public void SetSpot(string? spot)
        {
            Spot = spot;
        }

        // Getter and Setter for Head
        public string? GetHead()
        {
            return Head;
        }

        public void SetHead(string? head)
        {
            Head = head;
        }
    }
}