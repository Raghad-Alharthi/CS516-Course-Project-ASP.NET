using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.Models
{
    public class Class
    {
        [Key]
        public int ClassID { get; set; }

        [Required]
        public string ClassName { get; set; }

        [ForeignKey("Teacher")]
        public int? TeacherID { get; set; }
        public User Teacher { get; set; }

        public ICollection<Lecture> Lectures { get; set; } = new List<Lecture>();
    }
}
