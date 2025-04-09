using System.Collections.Generic;
using StudentManagementSystem.Models;
namespace StudentManagementSystem.Models.ViewModels;
public class StudentViewModel
{
    public int UserID { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public List<Class> AssignedClasses { get; set; }
}
