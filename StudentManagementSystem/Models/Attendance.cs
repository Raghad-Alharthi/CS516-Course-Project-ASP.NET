using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceID { get; set; }

        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public User Student { get; set; }

        [ForeignKey("Lecture")]
        public int LectureID { get; set; }
        public Lecture Lecture { get; set; }

        public bool IsPresent { get; set; }

        // 🔹 Sick Leave File (if student submits proof)
        public string SickLeaveFile { get; set; }

        // 🔹 Status: 'Pending', 'Accepted', or 'Rejected'
        public string SickLeaveStatus { get; set; } = "Pending";
    }
}
