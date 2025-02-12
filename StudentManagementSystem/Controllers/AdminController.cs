using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using System.Globalization;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly StudentManagementDBContext _context;

    public AdminController(StudentManagementDBContext context)
    {
        _context = context;
    }

    // 🌟 Admin Dashboard
    public async Task<IActionResult> Dashboard()
    {
        ViewBag.Classes = await _context.Classes.Include(c => c.Teacher).ToListAsync();
        ViewBag.Students = await _context.Users.Where(u => u.Role == "Student").ToListAsync();
        ViewBag.Teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();

        return View();
    }

    // 🌟 Manage Classes
    public async Task<IActionResult> ManageClasses()
    {
        var classes = await _context.Classes.Include(c => c.Teacher).ToListAsync();
        ViewBag.Teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();
        return View(classes);
    }

    [HttpPost]
    public async Task<IActionResult> AddClassWithSchedule(string className, int TeacherID, string selectedDay, string selectedTime)
    {
        // 🔹 Create a new class and save it first
        var newClass = new Class
        {
            ClassName = className,
            TeacherID = TeacherID // Ensure consistency with your Class model
        };

        _context.Classes.Add(newClass);
        await _context.SaveChangesAsync(); // 🔹 Save the class first to get ClassID

        // 🔹 Generate lectures after class is created
        int weeksInSemester = 15;
        DayOfWeek targetDay = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), selectedDay);
        TimeSpan lectureTime = TimeSpan.ParseExact(selectedTime, "hh\\:mm", CultureInfo.InvariantCulture);

        List<Lecture> lectures = new List<Lecture>();
        DateTime startDate = DateTime.Today;

        while (startDate.DayOfWeek != targetDay)
        {
            startDate = startDate.AddDays(1);
        }

        for (int i = 0; i < weeksInSemester; i++)
        {
            lectures.Add(new Lecture
            {
                ClassID = newClass.ClassID, // Now ClassID exists
                LectureDateTime = startDate.Date.Add(lectureTime)
            });

            startDate = startDate.AddDays(7);
        }

        _context.Lectures.AddRange(lectures);
        await _context.SaveChangesAsync();

        return RedirectToAction("ManageClasses");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteClass(int classId)
    {
        var classToDelete = await _context.Classes
            .Include(c => c.Lectures) // 🔹 Ensure lectures are loaded
            .FirstOrDefaultAsync(c => c.ClassID == classId);

        if (classToDelete != null)
        {
            // 🔹 Remove related student-class relationships
            var studentClasses = _context.StudentClasses.Where(sc => sc.ClassID == classId);
            _context.StudentClasses.RemoveRange(studentClasses);

            // 🔹 Remove all scheduled lectures linked to the class
            _context.Lectures.RemoveRange(classToDelete.Lectures);

            // 🔹 Finally, remove the class
            _context.Classes.Remove(classToDelete);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("ManageClasses");
    }


    // 🌟 Manage Students
    public async Task<IActionResult> ManageStudents()
    {
        var students = await _context.Users.Where(u => u.Role == "Student").ToListAsync();
        ViewBag.Classes = await _context.Classes.ToListAsync();
        return View(students);
    }

    [HttpPost]
    public async Task<IActionResult> AssignStudentToClass(int StudentID, int ClassID)
    {
        var studentClass = new StudentClass { StudentID = StudentID, ClassID = ClassID };
        _context.StudentClasses.Add(studentClass);
        await _context.SaveChangesAsync();
        return RedirectToAction("ManageStudents");
    }

    [HttpPost]
    public async Task<IActionResult> AddStudent(string firstName, string lastName, string username, string password)
    {
        var newStudent = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Student"
        };

        _context.Users.Add(newStudent);
        await _context.SaveChangesAsync();
        return RedirectToAction("ManageStudents");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteStudent(int StudentID)
    {
        var student = await _context.Users.FindAsync(StudentID);
        if (student != null)
        {
            _context.Users.Remove(student);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("ManageStudents");
    }

    // 🌟 Manage Teachers
    public async Task<IActionResult> ManageTeachers()
    {
        var teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();
        return View(teachers);
    }

    [HttpPost]
    public async Task<IActionResult> AddTeacher(string firstName, string lastName, string username, string password)
    {
        var newTeacher = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Teacher"
        };

        _context.Users.Add(newTeacher);
        await _context.SaveChangesAsync();
        return RedirectToAction("ManageTeachers");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTeacher(int TeacherID)
    {
        var teacher = await _context.Users.FindAsync(TeacherID);
        if (teacher != null)
        {
            _context.Users.Remove(teacher);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("ManageTeachers");
    }

    public async Task<IActionResult> ViewScheduledLectures(int ClassID)
    {
        var lectures = await _context.Lectures
            .Where(l => l.ClassID == ClassID)
            .OrderBy(l => l.LectureDateTime)
            .ToListAsync();

        ViewBag.Class = await _context.Classes.FindAsync(ClassID);
        return View(lectures);
    }
}
