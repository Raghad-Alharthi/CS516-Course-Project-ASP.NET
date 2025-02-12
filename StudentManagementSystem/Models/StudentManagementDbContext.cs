using Microsoft.EntityFrameworkCore;

namespace StudentManagementSystem.Models  // Ensure this matches your project namespace
{
    public partial class StudentManagementDBContext : DbContext
    {
        public StudentManagementDBContext(DbContextOptions<StudentManagementDBContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<StudentClass> StudentClasses { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Lecture> Lectures { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=RAGHADS_LAPTOP;Database=StudentManagementDB;Trusted_Connection=True;TrustServerCertificate=True;");
            }
        }
    }
}
