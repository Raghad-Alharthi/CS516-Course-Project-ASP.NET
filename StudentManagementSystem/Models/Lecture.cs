using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.Models
{
    public class Lecture
    {
        [Key]
        public int LectureID { get; set; }

        [ForeignKey("Class")]
        public int ClassID { get; set; }
        public Class Class { get; set; } // 🔹 Navigation Property

        [Required]
        public DateTime LectureDateTime { get; set; }
    }
}
