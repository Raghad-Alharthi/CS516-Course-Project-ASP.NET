using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Models;
using StudentManagementSystem.Models.ViewModels;
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

    // Manage Classes
    public async Task<IActionResult> ManageClasses(int? editId)
    {
        var classes = await _context.Classes.Include(c => c.Teacher).ToListAsync();
        var teachers = await _context.Users.Where(u => u.Role == "Teacher").ToListAsync();

        var firstLectures = _context.Lectures
            .GroupBy(l => l.ClassID)
            .Select(g => new { ClassID = g.Key, FirstLecture = g.Min(l => l.LectureDateTime) })
            .ToDictionary(g => g.ClassID, g => (DateTime?)g.FirstLecture);

        var vm = new ManageClassesViewModel
        {
            Classes = classes,
            ClassToEdit = editId.HasValue ? await _context.Classes.Include(c => c.Teacher).FirstOrDefaultAsync(c => c.ClassID == editId) : null,
            FirstLecturesByClassId = firstLectures
        };

        ViewBag.Teachers = teachers;
        ViewBag.Classes = classes;
        return View(vm);
    }



    [HttpPost]
    public async Task<IActionResult> AddClassWithSchedule(string className, int TeacherID, string selectedDay, string selectedTime)
    {
        var newClass = new Class
        {
            ClassName = className,
            TeacherID = TeacherID
        };

        DayOfWeek targetDay = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), selectedDay);
        TimeSpan lectureTime = TimeSpan.ParseExact(selectedTime, "hh\\:mm", CultureInfo.InvariantCulture);

        // Validate day
        if (targetDay == DayOfWeek.Friday || targetDay == DayOfWeek.Saturday)
        {
            TempData["Error"] = "Only Sunday to Thursday are allowed.";
            return RedirectToAction("ManageClasses");
        }

        // Validate time
        if (lectureTime < TimeSpan.FromHours(8) || lectureTime >= TimeSpan.FromHours(19))
        {
            TempData["Error"] = "Lecture must be between 08:00 and 19:00.";
            return RedirectToAction("ManageClasses");
        }

        // ✅ FIX: First fetch teacher lectures from DB (basic filter only)
        var possibleLectures = await _context.Lectures
            .Include(l => l.Class)
            .Where(l => l.Class.TeacherID == TeacherID)
            .ToListAsync();

        // ✅ Now apply the time overlap check in memory
        bool conflict = possibleLectures.Any(l =>
            l.LectureDateTime.DayOfWeek == targetDay &&
            Math.Abs((l.LectureDateTime.TimeOfDay - lectureTime).TotalMinutes) < 120
        );

        if (conflict)
        {
            TempData["Error"] = "Teacher already has a lecture at or overlapping with that time.";
            return RedirectToAction("ManageClasses");
        }

        // Save new class
        _context.Classes.Add(newClass);
        await _context.SaveChangesAsync();

        // Generate lectures
        int weeksInSemester = 15;
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
                ClassID = newClass.ClassID,
                LectureDateTime = startDate.Date.Add(lectureTime)
            });

            startDate = startDate.AddDays(7);
        }

        _context.Lectures.AddRange(lectures);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Class and lectures created successfully.";
        return RedirectToAction("ManageClasses");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteClass(int classId)
    {
        var classToDelete = await _context.Classes
            .Include(c => c.Lectures) 
                .ThenInclude(l => l.Attendances)
            .FirstOrDefaultAsync(c => c.ClassID == classId);

        if (classToDelete != null)
        {
            // Remove related student-class relationships
            var studentClasses = _context.StudentClasses.Where(sc => sc.ClassID == classId);
            _context.StudentClasses.RemoveRange(studentClasses);

            // Remove all related attendances linked to the lectures
            foreach (var lecture in classToDelete.Lectures)
            {
                var attendances = _context.Attendances.Where(a => a.LectureID == lecture.LectureID);
                _context.Attendances.RemoveRange(attendances);
            }

            // Remove all scheduled lectures linked to the class
            _context.Lectures.RemoveRange(classToDelete.Lectures);

            // Finally, remove the class
            _context.Classes.Remove(classToDelete);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("ManageClasses");
    }

    [HttpPost]
    public async Task<IActionResult> EditClass(int ClassID, int? TeacherID)
    {
        var classItem = await _context.Classes.FindAsync(ClassID);
        if (classItem == null) return NotFound();

        //check conflict
        var classLectures = await _context.Lectures
            .Where(l => l.ClassID == ClassID)
            .ToListAsync();

        var otherLectures = await _context.Lectures
            .Include(l => l.Class)
            .Where(l => l.Class.TeacherID == TeacherID && l.ClassID != ClassID)
            .ToListAsync(); // 🔁 pull data into memory

        foreach (var lecture in classLectures)
        {
            var overlap = otherLectures.Any(l =>
                l.LectureDateTime.DayOfWeek == lecture.LectureDateTime.DayOfWeek &&
                Math.Abs((l.LectureDateTime - lecture.LectureDateTime).TotalMinutes) < 120);

            if (overlap)
            {
                TempData["Error"] = "Teacher is not available for one or more scheduled lecture times.";
                return RedirectToAction("ManageClasses", new { editId = ClassID });
            }
        }



        classItem.TeacherID = TeacherID;

        await _context.SaveChangesAsync();
        TempData["Message"] = "Class updated successfully.";

        return RedirectToAction("ManageClasses");
    }


    // Manage Students
    public async Task<IActionResult> ManageStudents()
    {
        var students = await _context.Users
            .Where(u => u.Role == "Student")
            .Include(s => s.StudentClasses)
            .ThenInclude(sc => sc.Class)
            .ToListAsync();

        var classes = await _context.Classes.ToListAsync();

        var studentsWithClasses = students.Select(student => new StudentViewModel
        {
            UserID = student.UserID,
            FirstName = student.FirstName,
            LastName = student.LastName,
            AssignedClasses = student.StudentClasses.Select(sc => sc.Class).ToList()
        }).ToList();


        // ViewBag.Students = studentsWithClasses;
         ViewBag.Classes = classes;

        return View(studentsWithClasses);
    }


    [HttpPost]
    public async Task<IActionResult> AssignStudentToClass(int StudentID, int ClassID)
    {
        // Check if the assignment already exists
        bool alreadyAssigned = await _context.StudentClasses
            .AnyAsync(sc => sc.StudentID == StudentID && sc.ClassID == ClassID);

        if (alreadyAssigned)
        {
            TempData["Message"] = "Student is already assigned to this class.";
            return RedirectToAction("ManageStudents");
        }

        var studentClass = new StudentClass
        {
            StudentID = StudentID,
            ClassID = ClassID
        };

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
    public async Task<IActionResult> DeleteStudent(int id)
    {
        var student = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Student");
        if (student == null)
        {
            return NotFound();
        }

        var assignments = await _context.StudentClasses
            .Where(sc => sc.StudentID == id)
            .ToListAsync();

        _context.StudentClasses.RemoveRange(assignments);

        _context.Users.Remove(student);

        await _context.SaveChangesAsync();

        TempData["Message"] = "Student deleted successfully.";
        return RedirectToAction("ManageStudents");
    }


    // Manage Teachers
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
        // Step 1: Find the teacher
        var teacher = await _context.Users.FindAsync(TeacherID);
        if (teacher == null)
        {
            return NotFound();
        }

        // Step 2: Unassign this teacher from all classes
        var assignedClasses = await _context.Classes
            .Where(c => c.TeacherID == TeacherID)
            .ToListAsync();

        foreach (var c in assignedClasses)
        {
            c.TeacherID = null; // Remove the link to the teacher
        }

        // Step 3: Save changes before deleting the teacher
        await _context.SaveChangesAsync();

        // Step 4: Now it's safe to delete the teacher
        _context.Users.Remove(teacher);
        await _context.SaveChangesAsync();

        return RedirectToAction("ManageTeachers");
    }

    [HttpPost]
    public async Task<IActionResult> AssignTeacherToClass(int ClassID, int TeacherID)
    {
        var classEntity = await _context.Classes.FindAsync(ClassID);
        if (classEntity == null)
        {
            return NotFound();
        }

        classEntity.TeacherID = TeacherID;

        await _context.SaveChangesAsync();
        TempData["Message"] = "Teacher assigned successfully.";

        return RedirectToAction("ManageClasses");
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
