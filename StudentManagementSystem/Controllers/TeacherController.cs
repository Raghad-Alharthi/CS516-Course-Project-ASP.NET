using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Models;
using System.Linq;
using System.Threading.Tasks;
using StudentManagementSystem.Models.ViewModels;

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    private readonly StudentManagementDBContext _context;

    public TeacherController(StudentManagementDBContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Dashboard()
    {
        // Get the currently logged-in teacher's username
        string username = User.Identity.Name;

        // Find the teacher by username
        var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (teacher == null)
        {
            return Unauthorized(); // Redirect to login if teacher is not found
        }

        // Retrieve the classes assigned to the logged-in teacher
        var teacherClasses = await _context.Classes
            .Where(c => c.TeacherID == teacher.UserID)
            .ToListAsync();

        ViewBag.TeacherClasses = teacherClasses;

        return View();
    }

    // View lectures for a class (Only past & current)
    public async Task<IActionResult> ManageClass(int ClassID)
    {
        string username = User.Identity.Name;
        var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (teacher == null) return Unauthorized();

        var assignedClass = await _context.Classes
            .Where(c => c.ClassID == ClassID && c.TeacherID == teacher.UserID)
            .FirstOrDefaultAsync();

        if (assignedClass == null)
        {
            return Unauthorized();
        }

        var lectures = await _context.Lectures
            .Where(l => l.ClassID == ClassID && l.LectureDateTime <= DateTime.Now)
            .OrderByDescending(l => l.LectureDateTime)
            .ToListAsync();

        ViewBag.Class = assignedClass;
        return View(lectures);
    }

    // View Attendance Sheet for a Lecture
    public async Task<IActionResult> EditAttendance(int lectureId)
    {
        var lecture = await _context.Lectures
            .Include(l => l.Class)
            .FirstOrDefaultAsync(l => l.LectureID == lectureId);

        if (lecture == null) return NotFound();

        var attendanceRecords = await _context.Attendances
            .Where(a => a.LectureID == lectureId) // 🔹 Fixed Query
            .ToListAsync();

        var students = await _context.StudentClasses
            .Where(sc => sc.ClassID == lecture.ClassID)
            .Select(sc => sc.Student)
            .ToListAsync();

        var attendanceViewModel = students.Select(student => new AttendanceViewModel
        {
            StudentID = student.UserID,
            FullName = $"{student.FirstName} {student.LastName}",
            IsPresent = !attendanceRecords.Any(a => a.StudentID == student.UserID)
           || attendanceRecords.Any(a => a.StudentID == student.UserID && a.IsPresent)

        }).ToList();

        ViewBag.Lecture = lecture;
        return View(attendanceViewModel);
    }

    [HttpPost]
    public async Task<IActionResult> SaveAttendance(int lectureId, List<int> presentStudents)
    {
        var lecture = await _context.Lectures
            .Include(l => l.Class)
            .FirstOrDefaultAsync(l => l.LectureID == lectureId);

        if (lecture == null)
            return NotFound();

        var students = await _context.StudentClasses
            .Where(sc => sc.ClassID == lecture.ClassID)
            .Include(sc => sc.Student)
            .Select(sc => sc.Student)
            .Where(s => s != null)
            .ToListAsync();

        foreach (var student in students)
        {
            if (student == null || student.UserID == 0)
                continue;

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.LectureID == lectureId && a.StudentID == student.UserID);

            bool isNowPresent = presentStudents?.Contains(student.UserID) ?? false;

            if (attendance != null)
            {
                if (isNowPresent)
                {
                    // Student was previously marked absent but is now present — remove record
                    _context.Attendances.Remove(attendance);
                    if (!string.IsNullOrEmpty(attendance.SickLeaveFile))
                    {
                        var path = Path.Combine("wwwroot", attendance.SickLeaveFile.TrimStart('/'));
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);
                    }
                }
                else
                {
                    attendance.IsPresent = false; // still absent
                }
            }
            else
            {
                // Create new attendance record only if absent
                if (!isNowPresent)
                {
                    attendance = new Attendance
                    {
                        StudentID = student.UserID,
                        LectureID = lectureId,
                        IsPresent = false
                    };
                    _context.Attendances.Add(attendance);
                }
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Attendance has been successfully updated.";
        return RedirectToAction("ManageClass", new { ClassID = lecture.ClassID });
    }


    // View sick leave requests
    public async Task<IActionResult> SickLeaveRequests(int lectureId)
    {
        var requests = await _context.Attendances
            .Include(a => a.Student)
            .Include(a => a.Lecture)
            .Where(a => a.LectureID == lectureId && !string.IsNullOrEmpty(a.SickLeaveFile))
            .ToListAsync();

        ViewBag.LectureId = lectureId;
        return View(requests);
    }


    [HttpPost]
    public async Task<IActionResult> ApproveSickLeave(int attendanceId, string decision, string? comment)
    {
        var attendance = await _context.Attendances.FindAsync(attendanceId);
        if (attendance == null) return NotFound();

        if (decision == "Rejected" && string.IsNullOrWhiteSpace(comment))
        {
            TempData["Error"] = "You must provide a rejection comment.";
            return RedirectToAction("SickLeaveRequests", new { lectureId = attendance.LectureID });
        }

        attendance.SickLeaveStatus = decision;
        attendance.SickLeaveComment = decision == "Rejected" ? comment : null;

        await _context.SaveChangesAsync();
        TempData["Message"] = $"Sick leave request {decision.ToLower()}.";

        return RedirectToAction("SickLeaveRequests", new { lectureId = attendance.LectureID });
    }


}
