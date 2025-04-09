using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly StudentManagementDBContext _context;

    public StudentController(StudentManagementDBContext context)
    {
        _context = context;
    }

    // Student Dashboard - Shows Assigned Classes
    public async Task<IActionResult> Dashboard()
    {
        int studentId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var studentClasses = await _context.StudentClasses
            .Where(sc => sc.StudentID == studentId)
            .Include(sc => sc.Class)
            .ToListAsync();

        var absenceData = new List<object>();

        foreach (var studentClass in studentClasses)
        {
            int classId = studentClass.ClassID;
            string className = studentClass.Class.ClassName;

            int totalLectures = await _context.Lectures
                .Where(l => l.ClassID == classId)
                .CountAsync();

            int absentLectures = await _context.Attendances
                .Where(a => a.StudentID == studentId && a.Lecture.ClassID == classId && !a.IsPresent)
                .CountAsync();

            double absencePercentage = totalLectures > 0 ? ((double)absentLectures / totalLectures) * 100 : 0;

            absenceData.Add(new
            {
                ClassName = className,
                AbsencePercentage = absencePercentage,
                Absences = await _context.Attendances
                    .Where(a => a.StudentID == studentId && a.Lecture.ClassID == classId && !a.IsPresent)
                    .Include(a => a.Lecture)
                    .ToListAsync()
            });
        }

        ViewBag.AbsenceData = absenceData;
        return View();
    }

    // Redirects to either Sick Leave Submission or Tracking
    public async Task<IActionResult> AbsenceDetails(int attendanceId)
    {
        var attendance = await _context.Attendances
            .Include(a => a.Lecture)
            .FirstOrDefaultAsync(a => a.AttendanceID == attendanceId);

        if (attendance == null) return NotFound();

        if (string.IsNullOrEmpty(attendance.SickLeaveFile))
        {
            return RedirectToAction("SubmitSickLeaveForm", new { attendanceId });
        }
        else
        {
            return RedirectToAction("TrackSickLeave", new { attendanceId });
        }
    }

    // Student submits a sick leave file
    [HttpPost]
    public async Task<IActionResult> SubmitSickLeave(int attendanceId, IFormFile sickLeaveFile)
    {
        var attendance = await _context.Attendances.FindAsync(attendanceId);
        if (attendance == null || attendance.IsPresent) return NotFound();

        if (sickLeaveFile != null)
        {
            // Save file
            string fileName = Path.GetFileName(sickLeaveFile.FileName);
            string filePath = Path.Combine("wwwroot/sick_leaves", fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await sickLeaveFile.CopyToAsync(stream);
            }

            // Update status and file reference
            attendance.SickLeaveFile = "/sick_leaves/" + fileName;
            attendance.SickLeaveStatus = "Pending";

            _context.Update(attendance); 
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Dashboard");
    }


    // Sick Leave Form View
    public async Task<IActionResult> SubmitSickLeaveForm(int attendanceId)
    {
        var attendance = await _context.Attendances
            .Include(a => a.Lecture)
            .FirstOrDefaultAsync(a => a.AttendanceID == attendanceId);

        if (attendance == null) return NotFound();

        ViewBag.Attendance = attendance;
        return View();
    }

    // Track Sick Leave Submission Status
    public async Task<IActionResult> TrackSickLeave(int attendanceId)
    {
        var attendance = await _context.Attendances
            .Include(a => a.Lecture)
            .FirstOrDefaultAsync(a => a.AttendanceID == attendanceId);

        if (attendance == null) return NotFound();

        ViewBag.Attendance = attendance;
        return View();
    }
}
